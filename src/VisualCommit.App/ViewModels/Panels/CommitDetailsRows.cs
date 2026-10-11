using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>A parent in the details' Parents row: its short id, shown as a link that selects it in the graph.</summary>
public sealed class ParentLink
{
    internal ParentLink(string sha, Action<string> activate)
    {
        Sha = sha;
        ActivateCommand = new RelayCommand(() => activate(sha));
    }

    /// <summary>The parent's full id.</summary>
    public string Sha { get; }

    /// <summary>The first 7 characters of the id, as the graph's SHA column shows it.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>Raises the details' <see cref="CommitDetailsViewModel.ParentActivated"/> with <see cref="Sha"/>.</summary>
    public IRelayCommand ActivateCommand { get; }
}

/// <summary>
/// One row of a file list (a commit's changed files, or the stage panel's lists): a file, or in
/// the tree view also a folder. The list is rebuilt whole when it switches between flat and tree
/// or a folder opens or closes.
/// </summary>
public sealed partial class ChangedFileRow : ObservableObject
{
    /// <summary>Where the first column starts in a row at depth 0, in logical pixels.</summary>
    public const double FirstIndent = 12;

    /// <summary>How much further each level of the tree is indented.</summary>
    public const double IndentPerLevel = 14;

    private ChangedFileRow(string key, string name, int depth, Action<ChangedFileRow>? toggle)
    {
        Key = key;
        Name = name;
        Depth = depth;
        ToggleCommand = new RelayCommand(() => toggle?.Invoke(this), () => toggle is not null);
    }

    /// <summary>A file row: in the flat list with its folder, in the tree without.</summary>
    internal static ChangedFileRow ForFile(ChangedFile file, int depth, bool showFolder)
    {
        var slash = file.Path.LastIndexOf('/');
        return new ChangedFileRow(file.Path, file.Path[(slash + 1)..], depth, toggle: null)
        {
            File = file,
            Folder = showFolder && slash > 0 ? file.Path[..slash] : string.Empty,
        };
    }

    /// <summary>A folder row of the tree.</summary>
    internal static ChangedFileRow ForFolder(string path, string name, int depth, bool isOpen, Action<ChangedFileRow> toggle) =>
        new(path, name, depth, toggle) { IsOpen = isOpen };

    /// <summary>The file's path, or the folder's path in the tree. Names the row across rebuilds.</summary>
    public string Key { get; }

    /// <summary>The file's or folder's own name, without its folder.</summary>
    public string Name { get; }

    /// <summary>The level in the tree; 0 for every row of the flat list.</summary>
    public int Depth { get; }

    /// <summary>The space before the row's first column, in logical pixels.</summary>
    public double Indent => FirstIndent + (IndentPerLevel * Depth);

    /// <summary>The file this row stands for; null for a folder.</summary>
    public ChangedFile? File { get; private init; }

    public bool IsFolder => File is null;

    public bool IsFile => File is not null;

    /// <summary>A folder whose content is shown below it.</summary>
    public bool IsOpen { get; private init; }

    /// <summary>Opens or closes a folder of the tree; does nothing for a file.</summary>
    public IRelayCommand ToggleCommand { get; }

    /// <summary>The folder the file is in, shown after its name in the flat list; empty at the root and in the tree.</summary>
    public string Folder { get; private init; } = string.Empty;

    public bool HasFolder => Folder.Length > 0;

    /// <summary>The status letter: A, M, D, R, C, T, U for a conflict, or ? for a status the app does not know. Empty for a folder.</summary>
    public string StatusLetter => File?.Kind switch
    {
        null => string.Empty,
        FileChangeKind.Added => "A",
        FileChangeKind.Modified => "M",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R",
        FileChangeKind.Copied => "C",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.Conflicted => "U",
        _ => "?",
    };

    /// <summary>The row's file has its diff open: the row has the selection background.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The letter is drawn in the success colour.</summary>
    public bool IsAdded => File?.Kind == FileChangeKind.Added;

    /// <summary>The letter is drawn in the warning colour.</summary>
    public bool IsModified => File?.Kind == FileChangeKind.Modified;

    /// <summary>The letter is drawn in the danger colour: a deletion or a conflict.</summary>
    public bool IsDeleted => File?.Kind is FileChangeKind.Deleted or FileChangeKind.Conflicted;

    /// <summary>The letter is drawn in the accent colour.</summary>
    public bool IsRenamedOrCopied => File?.Kind is FileChangeKind.Renamed or FileChangeKind.Copied;

    /// <summary>"renamed from &lt;old path&gt;" (or "copied from") for a rename or copy; otherwise empty.</summary>
    public string OriginText => File switch
    {
        { Kind: FileChangeKind.Renamed, OldPath: { } old } => $"renamed from {old}",
        { Kind: FileChangeKind.Copied, OldPath: { } old } => $"copied from {old}",
        _ => string.Empty,
    };

    public bool HasOrigin => OriginText.Length > 0;

    /// <summary>The whole path, and where it came from, for the tooltip of a row cut off by a narrow panel.</summary>
    public string ToolTip => HasOrigin ? $"{Key}\n{OriginText}" : Key;
}
