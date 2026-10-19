using System.Diagnostics;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="RepositoryScanner"/> (docs/phase-6-design.md 1.8): every rule on a real folder tree
/// (folders with a <c>.git</c> folder or file; the scan reads only the file system, so no git is
/// needed), a link not followed while its target is found once, a Hidden or System folder left out
/// on Windows only; and on a fake <see cref="IScanFileSystem"/> what a real tree cannot give at
/// will: folders that cannot be listed, 12,000 folders, the token, the progress.
/// </summary>
public class RepositoryScannerTests
{
    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public void Candidates_are_found_to_depth_four_and_neither_opened_nor_the_chosen_folder_itself()
    {
        using var root = new TempDirectory("scan");
        Repository(root.Path);                                 // the chosen folder: never a candidate
        Repository(root.Combine("api"));                       // depth 1, a .git folder
        Repository(root.Combine("api", "nested"));             // inside a candidate: not opened
        GitFile(root.Combine("worktree"));                     // depth 1, a .git file (a linked worktree)
        Repository(root.Combine("a", "b", "c", "repo"));       // depth 4: found
        Repository(root.Combine("a", "b", "c", "d", "repo"));  // depth 5: not
        Repository(root.Combine("plain", "sub", "web"));       // depth 3
        Directory.CreateDirectory(root.Combine("empty", "folder"));

        var result = RepositoryScanner.Scan(root.Path, null, TestCancelled);

        Assert.Equal(
            [root.Combine("api"), root.Combine("worktree"), root.Combine("plain", "sub", "web"), root.Combine("a", "b", "c", "repo")],
            result.Candidates);
        Assert.False(result.StoppedAtLimit);

        // Taken from the queue: the root; a, api, empty, plain, worktree; a/b, empty/folder,
        // plain/sub; a/b/c, plain/sub/web; a/b/c/d and a/b/c/repo (depth 4, looked at, not opened).
        Assert.Equal(13, result.FoldersSearched);
    }

    [Fact]
    public void Tools_folders_ignoring_case_and_folders_whose_names_start_with_a_dot_are_left_out()
    {
        using var root = new TempDirectory("scan");
        foreach (var name in RepositoryScanner.LeftOutNames)
        {
            Repository(root.Combine(name, "inside"));
            Repository(root.Combine(name.ToUpperInvariant()[..1] + name[1..] + "-kept"));
        }

        Repository(root.Combine("Vendor", "inside"));
        Repository(root.Combine("NODE_MODULES", "inside"));
        Repository(root.Combine(".config", "inside"));
        Repository(root.Combine(".dotted"));
        Repository(root.Combine("src", "bin", "inside"));

        var result = RepositoryScanner.Scan(root.Path, null, TestCancelled);

        // On a file system that ignores case, Vendor and NODE_MODULES are the folders made above.
        Assert.Equal(
            RepositoryScanner.LeftOutNames.Select(name => root.Combine(name.ToUpperInvariant()[..1] + name[1..] + "-kept")).Order(StringComparer.Ordinal),
            result.Candidates);
    }

    [Fact]
    public void A_link_is_not_followed_while_its_target_is_found_once()
    {
        using var root = new TempDirectory("scan");
        Repository(root.Combine("projects", "api"));
        var link = root.Combine("link-to-projects");
        LinkFolder(link, root.Combine("projects"));
        try
        {
            Assert.True(Directory.Exists(Path.Combine(link, "api", ".git")));

            var result = RepositoryScanner.Scan(root.Path, null, TestCancelled);

            Assert.Equal([root.Combine("projects", "api")], result.Candidates);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void A_link_that_loops_back_to_the_chosen_folder_is_not_followed()
    {
        using var root = new TempDirectory("scan");
        Repository(root.Combine("deep", "repo"));
        var link = root.Combine("deep", "loop");
        LinkFolder(link, root.Path);
        try
        {
            var result = RepositoryScanner.Scan(root.Path, null, TestCancelled);

            Assert.Equal([root.Combine("deep", "repo")], result.Candidates);
            Assert.Equal(3, result.FoldersSearched);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void Hidden_and_system_folders_are_left_out_on_Windows_only()
    {
        using var root = new TempDirectory("scan");
        Repository(root.Combine("hidden", "repo"));
        Repository(root.Combine("system", "repo"));
        Repository(root.Combine("shown", "repo"));
        var hidden = new DirectoryInfo(root.Combine("hidden"));
        hidden.Attributes |= FileAttributes.Hidden;
        var system = new DirectoryInfo(root.Combine("system"));
        system.Attributes |= FileAttributes.System;
        try
        {
            var result = RepositoryScanner.Scan(root.Path, null, TestCancelled);

            // Windows' home folder keeps tools' clones in the hidden AppData (0.8); macOS's hidden
            // flag and Linux's lack of one are not 1.8's rule.
            Assert.Equal(
                OperatingSystem.IsWindows()
                    ? [root.Combine("shown", "repo")]
                    : [root.Combine("hidden", "repo"), root.Combine("shown", "repo"), root.Combine("system", "repo")],
                result.Candidates);
        }
        finally
        {
            hidden.Attributes &= ~FileAttributes.Hidden;
            system.Attributes &= ~FileAttributes.System;
        }
    }

    [Fact]
    public void The_physical_file_system_lists_folders_with_their_flags_and_finds_a_git_entry()
    {
        using var root = new TempDirectory("scan");
        Repository(root.Combine("repo"));
        GitFile(root.Combine("worktree"));
        Directory.CreateDirectory(root.Combine("plain"));
        File.WriteAllText(root.Combine("file.txt"), "not a folder\n");

        var folders = PhysicalScanFileSystem.Instance.ListFolders(root.Path);

        Assert.Equal(["plain", "repo", "worktree"], folders.Select(folder => folder.Name).Order(StringComparer.Ordinal));
        Assert.All(folders, folder => Assert.False(folder.IsLink || folder.IsHidden, folder.Name));
        Assert.True(PhysicalScanFileSystem.Instance.HasGitEntry(root.Combine("repo")));
        Assert.True(PhysicalScanFileSystem.Instance.HasGitEntry(root.Combine("worktree")));
        Assert.False(PhysicalScanFileSystem.Instance.HasGitEntry(root.Combine("plain")));
        Assert.False(PhysicalScanFileSystem.Instance.HasGitEntry(root.Combine("missing")));
        Assert.Throws<DirectoryNotFoundException>(() => PhysicalScanFileSystem.Instance.ListFolders(root.Combine("missing")));
    }

    [Fact]
    public void Folders_that_cannot_be_listed_are_passed_over_quietly()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.Add("/r/locked", failure: new UnauthorizedAccessException("Access denied."));
        fileSystem.Add("/r/busy", failure: new IOException("In use."));
        fileSystem.Add("/r/gone", failure: new DirectoryNotFoundException("Gone."));
        fileSystem.Add("/r/locked2/repo", isRepository: true);
        fileSystem.Add("/r/ok/repo", isRepository: true);
        fileSystem.Add("/r/locked2", failure: new UnauthorizedAccessException("Access denied."));

        var result = RepositoryScanner.Scan("/r", null, TestCancelled, fileSystem: fileSystem);

        Assert.Equal(["/r/ok/repo"], result.Candidates.Select(Unix));
        Assert.False(result.StoppedAtLimit);
    }

    [Fact]
    public void At_most_ten_thousand_folders_are_searched_and_what_was_found_is_kept()
    {
        var fileSystem = new FakeFileSystem();
        for (var i = 0; i < 12_000; i++)
        {
            fileSystem.Add($"/r/f{i:D5}", isRepository: i is 1 or 5 or 11_000);
        }

        var result = RepositoryScanner.Scan("/r", null, TestCancelled, fileSystem: fileSystem);

        Assert.True(result.StoppedAtLimit);
        Assert.Equal(10_000, result.FoldersSearched);
        Assert.Equal(["/r/f00001", "/r/f00005"], result.Candidates.Select(Unix));
    }

    [Fact]
    public void Exactly_ten_thousand_folders_do_not_stop_at_the_limit()
    {
        var fileSystem = new FakeFileSystem();
        for (var i = 0; i < 9_999; i++)
        {
            fileSystem.Add($"/r/f{i:D5}");
        }

        var result = RepositoryScanner.Scan("/r", null, TestCancelled, fileSystem: fileSystem);

        Assert.False(result.StoppedAtLimit);
        Assert.Equal(10_000, result.FoldersSearched);
    }

    [Fact]
    public void The_token_stops_the_search_at_the_next_folder()
    {
        using var cancel = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem { OnList = path => { if (Unix(path) == "/r/b") cancel.Cancel(); } };
        fileSystem.Add("/r/a/x");
        fileSystem.Add("/r/b/y");
        fileSystem.Add("/r/c/z");

        Assert.Throws<OperationCanceledException>(() => RepositoryScanner.Scan("/r", null, cancel.Token, fileSystem: fileSystem));

        // Breadth-first: the root, a and b were listed; nothing after b.
        Assert.Equal(["/r", "/r/a", "/r/b"], fileSystem.Listed.Select(Unix));
    }

    [Fact]
    public void Progress_comes_at_most_every_100_ms_and_once_at_the_end_with_the_counts()
    {
        var fileSystem = new FakeFileSystem { OnList = _ => Thread.Sleep(20) };
        for (var i = 0; i < 20; i++)
        {
            fileSystem.Add($"/r/f{i:D2}/inside", isRepository: i % 4 == 0);
        }

        var reports = new List<(ScanProgress Progress, TimeSpan At)>();
        var clock = Stopwatch.StartNew();
        var result = RepositoryScanner.Scan("/r", new SyncProgress(progress => reports.Add((progress, clock.Elapsed))), TestCancelled, fileSystem: fileSystem);

        // 36 folders listed (the root, the 20 folders, the 15 that are no repository) at 20 ms or
        // more each: reports before the last.
        Assert.True(reports.Count >= 2, $"{reports.Count} reports");
        Assert.Equal(new ScanProgress(result.FoldersSearched, result.Candidates.Count), reports[^1].Progress);
        Assert.Equal(new ScanProgress(41, 5), reports[^1].Progress);
        for (var i = 1; i < reports.Count - 1; i++)
        {
            Assert.True(reports[i].At - reports[i - 1].At >= TimeSpan.FromMilliseconds(99), $"Reports {i - 1} and {i} came {(reports[i].At - reports[i - 1].At).TotalMilliseconds} ms apart.");
            Assert.True(reports[i].Progress.FoldersSearched >= reports[i - 1].Progress.FoldersSearched);
        }
    }

    private static string Unix(string path) => path.Replace('\\', '/');

    private static void Repository(string folder) => Directory.CreateDirectory(Path.Combine(folder, ".git"));

    private static void GitFile(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ".git"), "gitdir: ../elsewhere/.git/worktrees/worktree\n");
    }

    /// <summary>
    /// Makes <paramref name="link"/> lead to the folder <paramref name="target"/>: a junction on
    /// Windows, which needs no privilege (a symbolic link needs Developer Mode or an administrator),
    /// a symbolic link elsewhere. <see cref="Directory.Delete(string)"/> removes either without
    /// touching the target.
    /// </summary>
    internal static void LinkFolder(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var start = new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/d", "/c", "mklink", "/J", link, target },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start.");

        // A line on each at most: neither pipe can fill while the other is read.
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link), $"mklink /J {link} {target} failed ({process.ExitCode}): {output}{error}");
    }

    /// <summary>Reports at once, on the scanning thread (<see cref="Progress{T}"/> would post).</summary>
    private sealed class SyncProgress(Action<ScanProgress> report) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => report(value);
    }

    /// <summary>A tree of folders in memory, by path with <c>/</c>; a folder can be a repository or fail to list.</summary>
    private sealed class FakeFileSystem : IScanFileSystem
    {
        private readonly Dictionary<string, List<string>> _children = new(StringComparer.Ordinal);
        private readonly HashSet<string> _repositories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

        public List<string> Listed { get; } = [];

        public Action<string>? OnList { get; init; }

        /// <summary>Adds <paramref name="path"/> and every folder above it.</summary>
        public void Add(string path, bool isRepository = false, Exception? failure = null)
        {
            var current = path;
            while (current != "/r")
            {
                var parent = current[..current.LastIndexOf('/')];
                var children = _children.TryGetValue(parent, out var list) ? list : _children[parent] = [];
                var name = current[(current.LastIndexOf('/') + 1)..];
                if (!children.Contains(name))
                {
                    children.Add(name);
                }

                current = parent;
            }

            if (isRepository)
            {
                _repositories.Add(path);
            }

            if (failure is not null)
            {
                _failures[path] = failure;
            }
        }

        public IReadOnlyList<ScanFolder> ListFolders(string path)
        {
            path = Unix(path);
            Listed.Add(path);
            OnList?.Invoke(path);
            if (_failures.TryGetValue(path, out var failure))
            {
                throw failure;
            }

            return _children.TryGetValue(path, out var children) ? children.Select(name => new ScanFolder(name, false, false)).ToList() : [];
        }

        public bool HasGitEntry(string path) => _repositories.Contains(Unix(path));
    }
}
