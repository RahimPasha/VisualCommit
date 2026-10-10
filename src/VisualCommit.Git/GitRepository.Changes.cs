using System.Globalization;
using System.Text;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    /// <summary>
    /// Options of every diff the app reads, which keep the user's configuration out of what is
    /// parsed and applied (D66): no colours, no external diff tool, no text conversion (a patch
    /// must be of the real content), git's own prefixes whatever <c>diff.noprefix</c> or
    /// <c>diff.mnemonicPrefix</c> say, and three lines of context whatever <c>diff.context</c> says.
    /// </summary>
    private static readonly string[] PlainDiffOptions =
        ["--no-color", "--no-ext-diff", "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/", "--unified=3"];

    public async Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        var output = await ReadOutputAsync(cancellationToken, "status", "--porcelain=v2", "-z", "--untracked-files=all", "--renames")
            .ConfigureAwait(false);
        return ParseStatus(output);
    }

    /// <summary>
    /// Reads <c>git status --porcelain=v2 -z</c>: ordinary entries (<c>1</c>), renames and copies
    /// (<c>2</c>, followed by the old path), unmerged entries (<c>u</c>) and untracked files
    /// (<c>?</c>). The first status letter is the index against HEAD, the second the working tree
    /// against the index; <c>.</c> means unchanged.
    /// </summary>
    internal static WorkingTreeStatus ParseStatus(string output)
    {
        var staged = new List<ChangedFile>();
        var unstaged = new List<ChangedFile>();
        var untracked = new HashSet<string>(StringComparer.Ordinal);
        var entries = output.Split('\0');
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 2)
            {
                continue;
            }

            switch (entry[0])
            {
                case '1':
                {
                    // 1 XY sub mH mI mW hH hI path
                    var fields = entry.Split(' ', 9);
                    if (fields.Length < 9)
                    {
                        continue;
                    }

                    AddEntry(fields[1], fields[8], oldPath: null);
                    break;
                }

                case '2':
                {
                    // 2 XY sub mH mI mW hH hI Xscore path, then the old path as the next entry.
                    var fields = entry.Split(' ', 10);
                    var oldPath = i + 1 < entries.Length ? entries[++i] : null;
                    if (fields.Length < 10)
                    {
                        continue;
                    }

                    AddEntry(fields[1], fields[9], oldPath);
                    break;
                }

                case 'u':
                {
                    // u XY sub m1 m2 m3 mW h1 h2 h3 path
                    var fields = entry.Split(' ', 11);
                    if (fields.Length == 11)
                    {
                        unstaged.Add(new ChangedFile(fields[10], FileChangeKind.Conflicted));
                    }

                    break;
                }

                case '?':
                {
                    var path = entry[2..];
                    unstaged.Add(new ChangedFile(path, FileChangeKind.Added));
                    untracked.Add(path);
                    break;
                }
            }
        }

        staged.Sort(ByPath);
        unstaged.Sort(ByPath);
        return new WorkingTreeStatus(staged, unstaged, untracked);

        void AddEntry(string xy, string path, string? oldPath)
        {
            if (xy[0] != '.')
            {
                var kind = KindOf(xy[0]);
                staged.Add(new ChangedFile(path, kind, kind is FileChangeKind.Renamed or FileChangeKind.Copied ? oldPath : null));
            }

            if (xy[1] != '.')
            {
                unstaged.Add(new ChangedFile(path, KindOf(xy[1])));
            }
        }

        static FileChangeKind KindOf(char letter) => letter switch
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

    /// <summary>The byte order of paths, as git sorts them: UTF-8 byte order and UTF-16 code-unit order agree outside surrogates.</summary>
    private static int ByPath(ChangedFile x, ChangedFile y) => string.CompareOrdinal(x.Path, y.Path);

    public async Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == FileChangeKind.Conflicted)
        {
            // Git prints a combined diff of the conflict's sides, which is phase 4's resolver's.
            return FileDiff.Empty;
        }

        var arguments = new List<string>();
        var exitCodes = new[] { 0 };
        switch (target.Side)
        {
            case DiffSide.Unstaged when target.IsUntracked:
                // An untracked file against nothing. --no-index exits with 1 when the files differ.
                arguments.AddRange(["diff", "--no-index"]);
                arguments.AddRange(PlainDiffOptions);
                arguments.AddRange(["--", "/dev/null", target.Path]);
                exitCodes = [0, 1];
                break;

            case DiffSide.Unstaged:
                arguments.Add("diff");
                arguments.AddRange(PlainDiffOptions);
                arguments.AddRange(["--", target.Path]);
                break;

            case DiffSide.Staged:
                arguments.AddRange(["diff", "--cached", "--find-renames"]);
                arguments.AddRange(PlainDiffOptions);
                arguments.AddRange(Paths(target));
                break;

            case DiffSide.Commit when target.Parent is null:
                // A root commit against nothing.
                arguments.AddRange(["diff-tree", "-p", "--root", "--no-commit-id", "--find-renames"]);
                arguments.AddRange(PlainDiffOptions);
                arguments.Add(target.Commit ?? throw new ArgumentException("A commit's diff needs the commit.", nameof(target)));
                arguments.AddRange(Paths(target));
                break;

            case DiffSide.Commit:
                arguments.AddRange(["diff", "--find-renames"]);
                arguments.AddRange(PlainDiffOptions);
                arguments.Add(target.Parent);
                arguments.Add(target.Commit ?? throw new ArgumentException("A commit's diff needs the commit.", nameof(target)));
                arguments.AddRange(Paths(target));
                break;
        }

        var command = new GitCommand([.. arguments]) { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironment, Encoding = GitCommand.Latin1 };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!exitCodes.Contains(result.ExitCode))
        {
            result.EnsureSuccess(command);
        }

        return DiffParser.Parse(result.StandardOutput);

        static string[] Paths(DiffTarget target) =>
            target.OldPath is { } old && old != target.Path ? ["--", target.Path, old] : ["--", target.Path];
    }

    public async Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var size = await ReadFileSizeAsync(version, cancellationToken).ConfigureAwait(false);
        if (size is null || size > maxBytes)
        {
            return null;
        }

        if (version.Source == FileVersionSource.WorkingTree)
        {
            return await File.ReadAllBytesAsync(WorkingTreePath(version.Path), cancellationToken).ConfigureAwait(false);
        }

        var command = new GitCommand("cat-file", "blob", ObjectName(version))
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            Encoding = GitCommand.Latin1,
        };
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return Encoding.Latin1.GetBytes(result.StandardOutput);
    }

    public async Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Source == FileVersionSource.WorkingTree)
        {
            var file = new FileInfo(WorkingTreePath(version.Path));
            return file.Exists ? file.Length : null;
        }

        var command = Read("cat-file", "-s", ObjectName(version));
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Succeeded && long.TryParse(result.StandardOutput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            ? size
            : null;
    }

    /// <summary>How git names a version kept in the index (<c>:path</c>, stage 0) or in a commit (<c>commit:path</c>).</summary>
    private static string ObjectName(FileVersion version) => version.Source switch
    {
        FileVersionSource.Index => ":" + version.Path,
        FileVersionSource.Commit => $"{version.Commit}:{version.Path}",
        _ => throw new ArgumentException("A file in the working tree has no object name.", nameof(version)),
    };

    /// <summary>The full path of a file of the working tree, from its path relative to the top folder.</summary>
    private string WorkingTreePath(string relativePath) =>
        Path.Combine(WorkingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
