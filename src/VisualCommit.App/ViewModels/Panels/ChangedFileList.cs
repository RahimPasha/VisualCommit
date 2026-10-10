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
        if (!keepFolders)
        {
            _closedFolders.Clear();
        }

        Rebuild();
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

    private void Rebuild()
    {
        List<ChangedFileRow> rows;
        if (Mode == FileListMode.Flat)
        {
            rows = [.. _files.Select(file => ChangedFileRow.ForFile(file, depth: 0, showFolder: true))];
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

        Rows.ReplaceAll(rows);
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
            rows.Add(ChangedFileRow.ForFile(file, depth, showFolder: false));
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
