using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// The reflog, its lost commits and the backups list (docs/phase-5-design.md 5.2, 5.3, 0.2, D180),
/// the backups on phase 4's parser (<c>GitRepository.Backups.cs</c>).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// What the reflog walk prints for each entry: its selector (<c>HEAD@{3}</c>), the commit, its
    /// parents, the entry's message and the commit's subject, separated by the unit separator.
    /// The subject comes last, so a separator inside it cannot shift the other fields.
    /// </summary>
    private const string ReflogFormat = "%gD%x1f%H%x1f%P%x1f%gs%x1f%s";

    private const int ReflogFieldCount = 5;

    /// <summary>
    /// What the second walk prints: with <c>--date=raw</c> the selector names the entry's own time
    /// (<c>HEAD@{1767268800 +0000}</c>) instead of its number, which no placeholder gives (0.2).
    /// </summary>
    private const string ReflogTimeFormat = "%gD%x1f%H";

    /// <summary>
    /// What <c>for-each-ref</c> prints for each backup ref, each field ended by NUL (a message's
    /// body spans lines): the ref, its object and that object's type, what a tag object points at
    /// and its type, the object's subject and body, and the subject of what a tag points at.
    /// </summary>
    private const string BackupFormat =
        "%(refname)%00%(objectname)%00%(objecttype)%00%(*objectname)%00%(*objecttype)%00" +
        "%(contents:subject)%00%(contents:body)%00%(*contents:subject)%00";

    private const int BackupFieldCount = 8;

    public async Task<ReflogResult> ReadReflogAsync(string fullRef, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullRef);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (fullRef.StartsWith('-'))
        {
            throw new ArgumentException("A ref name cannot start with '-'.", nameof(fullRef));
        }

        // --ignore-missing (before the ref) makes a ref without a reflog, or an unborn HEAD, no
        // output instead of "bad revision"; -n bounds the walk, one entry past the limit telling
        // whether there are more (ReflogResult.More).
        string[] walk = ["log", "--walk-reflogs", .. PlainLogOptions, "--ignore-missing", "-z", "-n", (limit + 1).ToString(CultureInfo.InvariantCulture)];
        for (var attempt = 1; ; attempt++)
        {
            // The two walks do not depend on each other: they run side by side.
            var entries = ReadOutputAsync(cancellationToken, [.. walk, "--format=" + ReflogFormat, fullRef, "--"]);
            var times = ReadOutputAsync(cancellationToken, [.. walk, "--date=raw", "--format=" + ReflogTimeFormat, fullRef, "--"]);
            await Task.WhenAll(entries, times).ConfigureAwait(false);

            if (ParseReflog(await entries.ConfigureAwait(false), await times.ConfigureAwait(false), _log, limit) is { } result)
            {
                return result;
            }

            // The two walks list different commits: the ref moved between them. Both are read once
            // more; a ref that moves again meanwhile fails the read, which the page's next refresh repeats.
            if (attempt == 2)
            {
                throw new GitException($"git log --walk-reflogs {fullRef}", 0, $"The reflog of {fullRef} changed while it was being read. Read it again.");
            }

            _log.Debug($"{Name}: The reflog of {fullRef} changed while it was read; reading it again.");
        }
    }

    /// <summary>
    /// Zips the two reflog walks (<see cref="ReflogFormat"/> and <see cref="ReflogTimeFormat"/>,
    /// <c>-z</c>) by position. Null when they do not list the same commits in the same order. An
    /// entry whose selector or commit cannot be read is left out and logged; the numbers git
    /// skipped (entries that name no commit that exists) are counted. At most
    /// <paramref name="limit"/> entries are kept: one more makes the result say there are more
    /// (<see cref="ReflogResult.More"/>), and a gap just before it is not counted.
    /// </summary>
    internal static ReflogResult? ParseReflog(string entriesOutput, string timesOutput, IAppLog log, int limit = int.MaxValue)
    {
        var entries = Records(entriesOutput);
        var times = Records(timesOutput);
        if (entries.Count != times.Count)
        {
            return null;
        }

        var result = new List<ReflogEntry>(Math.Min(entries.Count, limit));
        var skipped = 0;
        var expected = 0;
        var more = false;
        for (var i = 0; i < entries.Count; i++)
        {
            var fields = entries[i].Split('\x1f', ReflogFieldCount);
            var timeFields = times[i].Split('\x1f', 2);
            if (fields.Length != ReflogFieldCount || timeFields.Length != 2)
            {
                log.Warning($"Skipped a reflog entry that could not be read: {entries[i]}");
                continue;
            }

            var (selector, sha, parentsField, message, subject) = (fields[0], fields[1], fields[2], fields[3], fields[4]);
            if (sha != timeFields[1])
            {
                // Different commits at one position: the walks read different states of the reflog.
                return null;
            }

            if (!TryParseIds(sha, parentsField, out _, out var parents)
                || SelectorInside(selector) is not { } index
                || !int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || SelectorInside(timeFields[0]) is not { } raw
                || raw.Split(' ') is not [var seconds, var zone]
                || !GitRawDates.TryParse(seconds, zone, out var time))
            {
                log.Warning($"Skipped a reflog entry that could not be read: {entries[i]}");
                continue;
            }

            if (result.Count == limit)
            {
                more = true;
                continue;
            }

            // Git skips an entry whose commit it cannot read and keeps its number: a gap.
            skipped += Math.Max(0, number - expected);
            expected = number + 1;
            result.Add(new ReflogEntry(selector, number, sha, parents, message, subject, time));
        }

        return result.Count == 0 && skipped == 0 ? ReflogResult.Empty : new ReflogResult(result, skipped) { More = more };

        // -z ends each record with NUL, the last one too.
        static List<string> Records(string output)
        {
            var records = output.Split('\0').ToList();
            if (records.Count > 0 && records[^1].Length == 0)
            {
                records.RemoveAt(records.Count - 1);
            }

            return records;
        }

        // What lies between a selector's last "@{" and its closing brace: a ref name cannot hold "@{".
        static string? SelectorInside(string selector)
        {
            var open = selector.LastIndexOf("@{", StringComparison.Ordinal);
            return open < 0 || !selector.EndsWith('}') ? null : selector[(open + 2)..^1];
        }
    }

    public async Task<IReadOnlySet<string>> FindLostCommitsAsync(IReadOnlyCollection<string> shas, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shas);
        var asked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sha in shas)
        {
            if (!IsObjectId(sha))
            {
                throw new ArgumentException($"'{sha}' is not a full commit id.", nameof(shas));
            }

            asked.Add(sha);
        }

        var lost = new HashSet<string>(StringComparer.Ordinal);
        if (asked.Count == 0)
        {
            return lost;
        }

        // One walk from the SHAs that stops at everything a branch, tag, remote branch or HEAD
        // reaches: what it prints is lost, the asked SHAs among it included. --ignore-missing
        // skips a SHA whose commit is gone and an unborn HEAD; "--" keeps a file named HEAD from
        // making HEAD ambiguous.
        var command = new GitCommand("rev-list", "--ignore-missing", "--stdin", "--not", "--branches", "--tags", "--remotes", "HEAD", "--")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = string.Concat(asked.Select(sha => sha + "\n")),
            OnOutputLine = line =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (asked.Contains(line))
                {
                    lost.Add(line);
                }
            },
        };

        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return lost;
    }

    public async Task<IReadOnlyList<BackupInfo>> ReadBackupsAsync(CancellationToken cancellationToken = default)
    {
        var output = await ReadOutputAsync(cancellationToken, "for-each-ref", "--format=" + BackupFormat, BackupRefPrefix).ConfigureAwait(false);
        var backups = new Dictionary<string, (string Kind, int Number, int? Suffix, DateTimeOffset When, List<BackupRefInfo> Refs)>(StringComparer.Ordinal);

        // A tag of a tag: Git 2.36 peels "*" one step only (as ReadRefsAsync finds), Git 2.55 to its end.
        var nested = new List<(string Backup, int Index)>();
        var fields = output.Split('\0');
        for (var i = 0; i + BackupFieldCount <= fields.Length; i += BackupFieldCount)
        {
            // Each record but the first starts with the line feed that ended the one before.
            var refName = fields[i].TrimStart('\n');
            var (target, type, peeled, peeledType) = (fields[i + 1], fields[i + 2], fields[i + 3], fields[i + 4]);
            var (subject, body, peeledSubject) = (fields[i + 5], fields[i + 6].TrimEnd('\n'), fields[i + 7]);
            if (ParseBackupName(refName) is not { } name)
            {
                // A ref under the prefix that fits no scheme is not shown, as the clean-up leaves it.
                continue;
            }

            if (!backups.TryGetValue(name.Backup, out var backup))
            {
                // The name without a retry suffix is "<kind>-<time>"; the parser counts it as 1.
                var stem = $"{name.Kind}-{name.When.ToString(BackupTimeFormat, CultureInfo.InvariantCulture)}";
                backup = (name.Kind, name.Number, name.Backup == stem ? null : name.Number, name.When, []);
                backups.Add(name.Backup, backup);
            }

            // The commit a ref keeps: its own, a tag object's, or none for a tree or a blob.
            var (commit, commitSubject) = type switch
            {
                "commit" => (target, subject),
                "tag" when peeledType == "commit" => (peeled, peeledSubject),
                _ => (string.Empty, string.Empty),
            };
            if (type == "tag" && peeledType == "tag")
            {
                nested.Add((name.Backup, backup.Refs.Count));
            }

            // What follows "<kind>-<time>[-<n>]": nothing, "-changes" or "/<name>".
            var rest = refName[(BackupRefPrefix.Length + name.Backup.Length)..];
            backup.Refs.Add(new BackupRefInfo(refName, target, rest.StartsWith('/') ? rest[1..] : null, rest == "-changes", subject, body, commit, commitSubject));
        }

        foreach (var (name, index) in nested)
        {
            var refs = backups[name].Refs;
            refs[index] = await PeelToCommitAsync(refs[index], cancellationToken).ConfigureAwait(false);
        }

        // Newest first: the clean-up's order (time, number, kind), reversed. Within a backup, the
        // main or named refs in git's order of their names, then the -changes snapshot, which
        // sorts before a "/name" ref.
        return
        [
            .. backups
                .OrderByDescending(entry => entry.Value.When)
                .ThenByDescending(entry => entry.Value.Number)
                .ThenByDescending(entry => entry.Value.Kind, StringComparer.Ordinal)
                .Select(entry => new BackupInfo(
                    entry.Key,
                    entry.Value.Kind,
                    entry.Value.When,
                    entry.Value.Suffix,
                    [.. entry.Value.Refs.Where(info => !info.IsChanges), .. entry.Value.Refs.Where(info => info.IsChanges)])),
        ];
    }

    /// <summary>
    /// The commit a tag of a tag keeps, and its subject: <c>git log -1 &lt;ref&gt;^{commit}</c>.
    /// One call per such ref, which only a user's tag of a tag makes. A tag that ends at no commit
    /// (a tree or a blob) keeps none.
    /// </summary>
    private async Task<BackupRefInfo> PeelToCommitAsync(BackupRefInfo entry, CancellationToken cancellationToken)
    {
        var command = Read(["log", "-1", .. PlainLogOptions, "--format=%H%x1f%s", entry.FullName + "^{commit}", "--"]);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var fields = result.StandardOutput.TrimEnd('\n').Split('\x1f', 2);
        return result.Succeeded && fields.Length == 2 && IsObjectId(fields[0])
            ? entry with { Commit = fields[0], CommitSubject = fields[1] }
            : entry with { Commit = string.Empty, CommitSubject = string.Empty };
    }
}
