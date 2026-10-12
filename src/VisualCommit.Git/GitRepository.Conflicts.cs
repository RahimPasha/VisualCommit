using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Phase 4's conflict members (docs/phase-4-design.md 0.4, 11.3, 11.6 to 11.9, 5.1 "Markers",
/// D148, D149): what the resolver reads of a conflicted file, how a file is marked resolved, a
/// whole side or a deletion, the external merge tool, and the staged files that still have
/// markers. A side is never taken with <c>git restore --ours</c> (it deletes a file the side
/// lacks without a word), and nothing here runs <c>git checkout --ours</c> or <c>--theirs</c> on a
/// path that is not unmerged (it would overwrite the user's edits with the index's version).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>How many leading bytes of a blob git looks at for a NUL to call it binary.</summary>
    private const int BinaryProbeLength = 8000;

    /// <summary>A stage's mode for a symbolic link.</summary>
    private const string SymbolicLinkMode = "120000";

    /// <summary>A stage's mode for a submodule (a gitlink: a commit id, not a blob).</summary>
    private const string SubmoduleMode = "160000";

    public async Task<ConflictContent> ReadConflictAsync(ConflictEntry entry, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var markerSize = ReadMarkerSizeAsync(entry.Path, options, cancellationToken);
        var oursBinary = IsBinaryBlobAsync(entry.OursMode, entry.OursId, options, cancellationToken);
        var theirsBinary = IsBinaryBlobAsync(entry.TheirsMode, entry.TheirsId, options, cancellationToken);
        var workingTree = await ReadWorkingTreeEntryAsync(entry.Path, cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(markerSize, oursBinary, theirsBinary).ConfigureAwait(false);
        return new ConflictContent(
            workingTree ?? [],
            await markerSize.ConfigureAwait(false),
            await oursBinary.ConfigureAwait(false),
            await theirsBinary.ConfigureAwait(false),
            IsSymbolicLink: entry.OursMode == SymbolicLinkMode || entry.TheirsMode == SymbolicLinkMode,
            WorkingTreeExists: workingTree is not null);
    }

    public async Task<byte[]?> ReadConflictSideAsync(ConflictEntry entry, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (mode, id) = side == ConflictSide.Ours ? (entry.OursMode, entry.OursId) : (entry.TheirsMode, entry.TheirsId);
        if (ConflictEntry.IsMissing(id) || mode == SubmoduleMode)
        {
            return null;
        }

        // The working-tree form (0.4): the path's filters and line-end conversion applied, as
        // git checkout would write it; nothing is written. Byte for byte (D66).
        var command = new GitCommand("cat-file", "--filters", $"--path={entry.Path}", id)
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironmentWith(options),
            Encoding = GitCommand.Latin1,
        };
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return Encoding.Latin1.GetBytes(result.StandardOutput);
    }

    public async Task WriteResolutionAsync(string path, byte[] content, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        var full = WorkingTreePath(path);

        // Writing through a link would change the file it points at, perhaps outside the
        // working tree; a text conflict's file is never one.
        if (IsSymbolicLinkEntry(full))
        {
            throw new IOException($"{path} is a symbolic link: its result is not written through it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, cancellationToken).ConfigureAwait(false);
        await RunWriteAsync(options, cancellationToken, "add", "--", path).ConfigureAwait(false);
        _log.Info($"Wrote the resolution of {path} ({Count(content.Length, "byte")}) and staged it.");
    }

    public async Task TakeSideAsync(string path, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stages = await ReadUnmergedStagesAsync(path, options, cancellationToken).ConfigureAwait(false);
        var full = WorkingTreePath(path);
        var sideOption = side == ConflictSide.Ours ? "--ours" : "--theirs";
        if (stages.ContainsKey(2) && stages.ContainsKey(3))
        {
            // Both sides have the file (both modified, both added): a binary file or a symbolic
            // link (11.8). Checking out a side overwrites the working-tree file, edits and all, so
            // a file that differs from that side is kept in a snapshot first (D67's scheme).
            var wanted = stages[side == ConflictSide.Ours ? 2 : 3].Id;
            if (IsFileOrLink(full) && !string.Equals(await HashWorkingTreeEntryAsync(path, options, cancellationToken).ConfigureAwait(false), wanted, StringComparison.Ordinal))
            {
                await SaveSnapshotAsync([path], cancellationToken).ConfigureAwait(false);
            }

            await RunWriteAsync(options, cancellationToken, "checkout", sideOption, "--", path).ConfigureAwait(false);
        }
        else if (!IsFileOrLink(full))
        {
            // One side has the file (deleted by them or by us, added by one side, a file moved
            // aside by a file/folder clash) and it is gone from the working tree: that side's
            // version is written back. Git refuses a side that lacks the file.
            await RunWriteAsync(options, cancellationToken, "checkout", sideOption, "--", path).ConfigureAwait(false);
        }

        // Otherwise the file git left (with any edit the user made to it) is what is kept.
        await RunWriteAsync(options, cancellationToken, "add", "--", path).ConfigureAwait(false);
        _log.Info($"Resolved {path} with {(side == ConflictSide.Ours ? "our" : "their")} side.");
    }

    public async Task RemoveConflictedAsync(string path, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stages = await ReadUnmergedStagesAsync(path, options, cancellationToken).ConfigureAwait(false);

        // git rm deletes the working-tree file of a conflicted path whatever it holds (Git 2.36):
        // a file the user edited after git left it is kept in a snapshot first (D67's scheme).
        if (IsFileOrLink(WorkingTreePath(path)))
        {
            var hash = await HashWorkingTreeEntryAsync(path, options, cancellationToken).ConfigureAwait(false);
            if (!stages.Values.Any(stage => string.Equals(stage.Id, hash, StringComparison.Ordinal)))
            {
                await SaveSnapshotAsync([path], cancellationToken).ConfigureAwait(false);
            }
        }

        await RunWriteAsync(options, cancellationToken, "rm", "--", path).ConfigureAwait(false);
        _log.Info($"Resolved {path} by deleting it.");
    }

    public async Task<string?> ReadMergeToolAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        var command = new GitCommand("config", "--get", "merge.tool") { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironmentWith(options) };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null;
        }

        var tool = result.EnsureSuccess(command).StandardOutput.Trim();
        return tool.Length > 0 ? tool : null;
    }

    public async Task<MergeToolResult> RunMergeToolAsync(string path, string tool, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);

        // Git runs no tool for a path that is not unmerged ("does not need merging"), nor for a
        // submodule: refused before git runs.
        await ReadUnmergedStagesAsync(path, options, cancellationToken).ConfigureAwait(false);
        var leftovers = new MergeToolLeftovers(WorkingTreePath(path));
        try
        {
            // The -c comes before the subcommand: after it, git mergetool prints its usage (0.4).
            // keepBackup=false: a tool that succeeds leaves no backup of the file with markers.
            var command = WriteCommand(options, "-c", "mergetool.keepBackup=false", "mergetool", $"--tool={tool}", "--no-prompt", "--", path);
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if ((await ReadUnmergedEntriesAsync(path, options, cancellationToken).ConfigureAwait(false)).Count == 0)
            {
                _log.Info($"The merge tool {tool} resolved {path}.");
                return new MergeToolResult(Staged: true);
            }

            // The tool ran and did not resolve the file: git says "merge of <p> failed" and puts
            // the file back as it was. Any other failure means the tool did not run (git cannot
            // find it, or does not know it): git's own text explains it.
            if (result.Succeeded || ToolRanAndFailed(result))
            {
                _log.Info($"The merge tool {tool} closed without resolving {path}.");
                return new MergeToolResult(Staged: false);
            }

            result.EnsureSuccess(command);
            return new MergeToolResult(Staged: false);
        }
        finally
        {
            // Git leaves its copies beside the file when it cannot run the tool, when the user
            // keeps them (mergetool.keepTemporaries) and when it is stopped; a cancel kills it
            // before its own clean-up.
            leftovers.Remove(_log);
        }

        static bool ToolRanAndFailed(GitResult result) =>
            SplitLines(result.StandardError).Concat(SplitLines(result.StandardOutput))
                .Any(line => line.StartsWith("merge of ", StringComparison.Ordinal) && line.EndsWith(" failed", StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<string>> ReadStagedConflictMarkersAsync(IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            // Without a path git would check every staged file.
            return [];
        }

        var command = new GitCommand(["diff", "--cached", "--check", "--no-color", "--no-ext-diff", "--", .. paths])
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironmentWith(options),
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);

        // Exit 2: git found a problem (a marker, or a whitespace error, which is not asked about).
        if (result.ExitCode is not (0 or 2))
        {
            result.EnsureSuccess(command);
        }

        // "<path>:<line>: leftover conflict marker", the path as it is (never quoted).
        const string Leftover = ": leftover conflict marker";
        var marked = SplitLines(result.StandardOutput)
            .Where(line => line.EndsWith(Leftover, StringComparison.Ordinal))
            .Select(line => line[..^Leftover.Length])
            .ToList();
        return [.. paths.Distinct(StringComparer.Ordinal).Where(path => marked.Any(line => IsLineOf(line, path)))];

        static bool IsLineOf(string line, string path) =>
            line.Length > path.Length + 1
            && line.StartsWith(path, StringComparison.Ordinal)
            && line[path.Length] == ':'
            && line.AsSpan(path.Length + 1).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>The environment of a call that only reads, with the operation's prompt variables (a filter may ask).</summary>
    private static Dictionary<string, string?> ReadEnvironmentWith(NetworkOptions? options)
    {
        var environment = new Dictionary<string, string?>((options ?? NetworkOptions.None).Environment);
        foreach (var (name, value) in ReadEnvironment)
        {
            environment[name] = value;
        }

        return environment;
    }

    /// <summary>
    /// The conflict marker size git uses for <paramref name="path"/> (11.3): the attribute
    /// <c>conflict-marker-size</c> from <c>git check-attr -z</c>, read as git reads it (a positive
    /// number; anything else, unset or unspecified, is the default 7).
    /// </summary>
    private async Task<int> ReadMarkerSizeAsync(string path, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var command = new GitCommand("check-attr", "-z", "conflict-marker-size", "--", path) { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironmentWith(options) };
        var output = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;

        // "<path>\0conflict-marker-size\0<value>\0"
        var fields = output.Split('\0');
        return fields.Length >= 3 && LeadingNumber(fields[2]) is > 0 and var size ? size : ConflictText.DefaultMarkerSize;

        // As C's atoi, which git uses: leading digits, after an optional sign.
        static int LeadingNumber(string value)
        {
            var digits = value.TrimStart();
            var sign = digits.StartsWith('-') ? -1 : 1;
            digits = digits.TrimStart('+', '-');
            var end = 0;
            while (end < digits.Length && end < 9 && char.IsAsciiDigit(digits[end]))
            {
                end++;
            }

            return end == 0 ? 0 : sign * int.Parse(digits.AsSpan(0, end), NumberStyles.None, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Whether a stage's blob is binary as git decides for a merge (11.8): a NUL in its first
    /// 8000 bytes. A missing stage, a symbolic link and a submodule are not. The blob is streamed
    /// and only its start is looked at, so a large one is never held in memory.
    /// </summary>
    private async Task<bool> IsBinaryBlobAsync(string mode, string id, NetworkOptions? options, CancellationToken cancellationToken)
    {
        if (ConflictEntry.IsMissing(id) || mode is SymbolicLinkMode or SubmoduleMode)
        {
            return false;
        }

        // The runner hands the output over line by line; a line's position counts its line feed
        // (a carriage return before it is not handed over, so a NUL a few bytes past 8000 in a
        // file with CRLF line ends can count as inside: the safe side, no markers are offered).
        var seen = 0;
        var binary = false;
        var command = new GitCommand("cat-file", "blob", id)
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironmentWith(options),
            Encoding = GitCommand.Latin1,
            OnOutputLine = line =>
            {
                if (binary || seen >= BinaryProbeLength)
                {
                    return;
                }

                var nul = line.IndexOf('\0', StringComparison.Ordinal);
                binary = nul >= 0 && seen + nul < BinaryProbeLength;
                seen += line.Length + 1;
            },
        };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return binary;
    }

    /// <summary>
    /// The working-tree entry of <paramref name="path"/> as git would hash it: a file's bytes, a
    /// symbolic link's target; null when there is none (or a folder stands there).
    /// </summary>
    private async Task<byte[]?> ReadWorkingTreeEntryAsync(string path, CancellationToken cancellationToken)
    {
        var full = WorkingTreePath(path);
        if (LinkTarget(full) is { } target)
        {
            return Encoding.UTF8.GetBytes(target);
        }

        try
        {
            return File.Exists(full) ? await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false) : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// The blob id git would give the working-tree entry of <paramref name="path"/>: a file
    /// through <c>git hash-object -- &lt;p&gt;</c> (its clean filter and line-end conversion
    /// applied, as <c>git add</c> would), a symbolic link from its target (<c>git hash-object
    /// --stdin --no-filters</c>: hash-object would follow the link).
    /// </summary>
    private async Task<string> HashWorkingTreeEntryAsync(string path, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var target = LinkTarget(WorkingTreePath(path));
        var command = new GitCommand(target is null ? ["hash-object", "--", path] : ["hash-object", "--stdin", "--no-filters"])
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironmentWith(options),
            StandardInput = target,
        };
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return result.StandardOutput.Trim();
    }

    /// <summary>
    /// The unmerged stages of exactly <paramref name="path"/> by number (1 base, 2 ours, 3 theirs):
    /// <c>git ls-files --unmerged -z -- &lt;p&gt;</c>, whose lines are
    /// <c>&lt;mode&gt; &lt;id&gt; &lt;stage&gt;\t&lt;path&gt;</c>. Paths below a folder of that name
    /// (a file/folder clash) are not its. A path that is not unmerged, or a submodule, is refused:
    /// nothing here may run on it.
    /// </summary>
    private async Task<Dictionary<int, (string Mode, string Id)>> ReadUnmergedStagesAsync(string path, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var stages = await ReadUnmergedEntriesAsync(path, options, cancellationToken).ConfigureAwait(false);
        if (stages.Count == 0)
        {
            throw new IOException($"{path} has no conflict now: nothing was changed.");
        }

        if (stages.Values.Any(stage => stage.Mode == SubmoduleMode))
        {
            throw new IOException($"{path} is a submodule: resolve it in a terminal.");
        }

        return stages;
    }

    private async Task<Dictionary<int, (string Mode, string Id)>> ReadUnmergedEntriesAsync(string path, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var command = new GitCommand("ls-files", "--unmerged", "-z", "--", path) { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironmentWith(options) };
        var output = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;
        var stages = new Dictionary<int, (string Mode, string Id)>();
        foreach (var entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t', StringComparison.Ordinal);
            var fields = tab < 0 ? [] : entry[..tab].Split(' ');
            if (fields.Length == 3 && string.Equals(entry[(tab + 1)..], path, StringComparison.Ordinal)
                && int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var stage))
            {
                stages[stage] = (fields[0], fields[1]);
            }
        }

        return stages;
    }

    /// <summary>A symbolic link's target, or null when <paramref name="fullPath"/> is no link (or is missing).</summary>
    private static string? LinkTarget(string fullPath)
    {
        try
        {
            return new FileInfo(fullPath).LinkTarget;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsSymbolicLinkEntry(string fullPath) => LinkTarget(fullPath) is not null;

    /// <summary>
    /// The copies <c>git mergetool</c> writes beside the file (11.9):
    /// <c>&lt;name&gt;_BACKUP_&lt;pid&gt;&lt;ext&gt;</c>, <c>_BASE_</c>, <c>_LOCAL_</c> and
    /// <c>_REMOTE_</c>, where git splits the file's name at its last dot (<c>a.tar.gz</c>:
    /// <c>a.tar_BASE_123.gz</c>; <c>Makefile</c>: <c>Makefile_BASE_123</c>). Those there before the
    /// run are the user's and stay.
    /// </summary>
    private sealed class MergeToolLeftovers
    {
        private readonly string _folder;
        private readonly Regex _pattern;
        private readonly HashSet<string> _before;

        public MergeToolLeftovers(string fullPath)
        {
            _folder = Path.GetDirectoryName(fullPath)!;
            var name = Path.GetFileName(fullPath);
            var dot = name.LastIndexOf('.');
            var (stem, extension) = dot < 0 ? (name, string.Empty) : (name[..dot], name[dot..]);
            _pattern = new Regex(
                $"^{Regex.Escape(stem)}_(BACKUP|BASE|LOCAL|REMOTE)_[0-9]+{Regex.Escape(extension)}$",
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            _before = Current();
        }

        public void Remove(IAppLog log)
        {
            foreach (var file in Current().Except(_before))
            {
                try
                {
                    File.Delete(file);
                    log.Info($"Removed {Path.GetFileName(file)}, left by the merge tool.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Warning($"Could not remove {file}, left by the merge tool.", ex);
                }
            }
        }

        private HashSet<string> Current()
        {
            try
            {
                return Directory.Exists(_folder)
                    ? Directory.EnumerateFiles(_folder).Where(file => _pattern.IsMatch(Path.GetFileName(file))).ToHashSet(StringComparer.Ordinal)
                    : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }
}
