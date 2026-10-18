using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The rows of a list of changed files, flat or as a tree (D44): what the commit details' file
/// list and the stage panel's two lists show. Flat, the files keep the order they are given in
/// (git's: by path); as a tree, folders come first and then files, each sorted by name, and a
/// folder the user closed stays closed while the list shows the same files.
/// </summary>
public sealed class ChangedFileList
{
    private readonly HashSet<string> _closedFolders = new(StringComparer.Ordinal);
    private IReadOnlyList<ChangedFile> _files = [];
    private IReadOnlySet<string> _unchanged = new HashSet<string>(StringComparer.Ordinal);
    private string? _selectedPath;

    /// <summary>The rows the view shows.</summary>
    public RowCollection<ChangedFileRow> Rows { get; } = [];

    /// <summary>The files, in the order they were given.</summary>
    public IReadOnlyList<ChangedFile> Files => _files;

    public FileListMode Mode { get; private set; }

    /// <summary>The path of the file whose row has the selection background, or null.</summary>
    public string? SelectedPath
    {
        get => _selectedPath;
        set
        {
            _selectedPath = value;
            foreach (var row in Rows)
            {
                row.IsSelected = row.IsFile && row.Key == value;
            }
        }
    }

    /// <summary>
    /// Shows <paramref name="files"/>. With <paramref name="keepFolders"/>, folders closed before
    /// stay closed (the same list read again); otherwise every folder starts open.
    /// </summary>
    public void Show(IReadOnlyList<ChangedFile> files, bool keepFolders = false)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
        _unchanged = new HashSet<string>(StringComparer.Ordinal);
        if (!keepFolders)
        {
            _closedFolders.Clear();
        }

        // The same list read again (the stage panel after every status read): a row that shows what
        // it showed stays, with its container and button, so a read that changed nothing touches no
        // row and a click on a row that a refresh reaches between its press and release still counts.
        Rebuild(keepRows: keepFolders);
    }

    /// <summary>
    /// Shows every file of a commit's tree (docs/phase-5-design.md 4.5, "All files"):
    /// <paramref name="tree"/>'s paths in its order (git's, by path), each that
    /// <paramref name="changed"/> lists with its status letter, the others without one. A changed
    /// file the tree no longer has (a deletion) is listed too, with its letter, at its path's
    /// place, so that its diff still opens from the list. Every folder starts open.
    /// </summary>
    public void ShowAll(IReadOnlyList<ChangedFile> changed, IReadOnlyList<string> tree)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(tree);
        var byPath = new Dictionary<string, ChangedFile>(StringComparer.Ordinal);
        foreach (var file in changed)
        {
            byPath.TryAdd(file.Path, file);
        }

        var inTree = new HashSet<string>(tree, StringComparer.Ordinal);
        var gone = changed.Where(file => !inTree.Contains(file.Path)).OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        var unchanged = new HashSet<string>(StringComparer.Ordinal);
        var files = new List<ChangedFile>(tree.Count + gone.Count);
        var next = 0;
        foreach (var path in tree)
        {
            // Git lists a tree by path, byte by byte: a deleted path goes before the first that sorts after it.
            for (; next < gone.Count && string.CompareOrdinal(gone[next].Path, path) < 0; next++)
            {
                files.Add(gone[next]);
            }

            files.Add(byPath.TryGetValue(path, out var file) ? file : Unchanged(path, unchanged));
        }

        files.AddRange(gone.Skip(next));
        _files = files;
        _unchanged = unchanged;
        _closedFolders.Clear();
        Rebuild();

        static ChangedFile Unchanged(string path, HashSet<string> unchanged)
        {
            unchanged.Add(path);
            return new ChangedFile(path, FileChangeKind.Unknown);
        }
    }

    /// <summary>Shows the files flat or as a tree.</summary>
    public void SetMode(FileListMode mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            Rebuild();
        }
    }

    private void Rebuild(bool keepRows = false)
    {
        List<ChangedFileRow> rows;
        if (Mode == FileListMode.Flat)
        {
            rows = [.. _files.Select(file => ChangedFileRow.ForFile(file, depth: 0, showFolder: true, _unchanged.Contains(file.Path)))];
        }
        else
        {
            var root = new FileFolder();
            foreach (var file in _files)
            {
                var parts = file.Path.Split('/');
                var folder = root;
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    if (!folder.Folders.TryGetValue(parts[i], out var child))
                    {
                        child = new FileFolder();
                        folder.Folders.Add(parts[i], child);
                    }

                    folder = child;
                }

                folder.Files.Add(file);
            }

            rows = [];
            AddFolder(rows, root, path: string.Empty, depth: 0);
        }

        foreach (var row in rows)
        {
            row.IsSelected = row.IsFile && row.Key == _selectedPath;
        }

        if (keepRows)
        {
            Rows.ReplaceAll(rows, (shown, rebuilt) => shown.ShowsSameAs(rebuilt));
        }
        else
        {
            Rows.ReplaceAll(rows);
        }
    }

    /// <summary>Adds a folder's content: its subfolders first, then its files, each sorted by name.</summary>
    private void AddFolder(List<ChangedFileRow> rows, FileFolder folder, string path, int depth)
    {
        foreach (var (name, child) in folder.Folders)
        {
            var childPath = path.Length == 0 ? name : path + "/" + name;
            var open = !_closedFolders.Contains(childPath);
            rows.Add(ChangedFileRow.ForFolder(childPath, name, depth, open, ToggleFolder));
            if (open)
            {
                AddFolder(rows, child, childPath, depth + 1);
            }
        }

        foreach (var file in folder.Files.OrderBy(file => file.Path[(file.Path.LastIndexOf('/') + 1)..], NameOrder.Instance))
        {
            rows.Add(ChangedFileRow.ForFile(file, depth, showFolder: false, _unchanged.Contains(file.Path)));
        }
    }

    private void ToggleFolder(ChangedFileRow row)
    {
        if (!_closedFolders.Remove(row.Key))
        {
            _closedFolders.Add(row.Key);
        }

        Rebuild();
    }

    private sealed class FileFolder
    {
        public SortedDictionary<string, FileFolder> Folders { get; } = new(NameOrder.Instance);

        public List<ChangedFile> Files { get; } = [];
    }
}
