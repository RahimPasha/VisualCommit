using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.Services;

/// <summary>
/// Keeps the workspaces in <c>workspaces.json</c> (D189, D203, docs/phase-6-design.md 1.3), as
/// <see cref="JsonSessionStore"/> keeps the session, except that it never writes over a file it
/// could not read:
/// <list type="bullet">
/// <item>No file: an empty list; the first change writes the file.</item>
/// <item>Bad JSON, or a read that fails (one that throws <see cref="IOException"/> is tried twice
/// more, <see cref="ReadRetryDelay"/> apart, as a virus scanner or a backup may hold the file a
/// moment): the file is moved aside to the first free name of <c>workspaces.unreadable.json</c>,
/// <c>workspaces.unreadable-2.json</c>, … (never over an earlier copy) and the list is empty, with
/// <see cref="SetAsideNotice"/>; when the move fails too, the file stays where it is, nothing writes
/// over it this session (<see cref="WorkspaceReadOnlyCause.Unreadable"/>) and the next start reads
/// it again.</item>
/// <item>A newer version's file: what this version understands is read, and nothing is saved
/// (<see cref="WorkspaceReadOnlyCause.NewerVersion"/>), so its bytes stay as they are; also when
/// this version cannot read it at all (a newer version changed its shape): then the list is empty,
/// and the file is neither set aside nor written over.</item>
/// <item>A failed save: the change applies for the session, <see cref="SaveFailed"/> and
/// <see cref="SaveFailedNotice"/> until a save succeeds, which writes the whole list.</item>
/// </list>
/// What it reads is cleaned as a hand-edited file needs (blank and repeated ids, blank names and
/// paths, a path a workspace holds already); the saved texts are never rewritten otherwise.
/// </summary>
public sealed class JsonWorkspaceStore : IWorkspaceStore
{
    /// <summary>The pause between two reads of a file that another program held (1.3).</summary>
    public static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>How often a read that throws <see cref="IOException"/> is tried, the first included.</summary>
    private const int ReadAttempts = 3;

    private readonly Lock _gate = new();
    private readonly string _filePath;
    private readonly IAppLog _log;
    private readonly IWorkspaceFiles _files;
    private readonly string? _loadNotice;

    /// <param name="filePath">The file, <c>AppPaths.WorkspacesFile</c> in the app.</param>
    /// <param name="log">The app's log.</param>
    /// <param name="files">How the file is read, written and moved; the disk unless a test fakes a failure.</param>
    public JsonWorkspaceStore(string filePath, IAppLog? log = null, IWorkspaceFiles? files = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _log = log ?? NullAppLog.Instance;
        _files = files ?? PhysicalWorkspaceFiles.Instance;
        (Current, ReadOnlyCause, _loadNotice) = Load();
        Notice = _loadNotice;
    }

    /// <summary>The notice text of a file that could not be read and was set aside as <paramref name="file"/> (a file name).</summary>
    public static string SetAsideNotice(string file) => $"workspaces.json could not be read, so it was set aside as {file}.";

    /// <summary>The notice of a file that could be neither read nor set aside: the store is read-only.</summary>
    public static string UnreadableNotice => "workspaces.json could not be read, so changes to workspaces are not saved this session.";

    /// <summary>The notice of a newer version's file: the store is read-only.</summary>
    public static string NewerVersionNotice => "workspaces.json was made by a newer version of VisualCommit, so workspaces cannot be changed in this version.";

    /// <summary>The notice while the last save failed.</summary>
    public static string SaveFailedNotice => "workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.";

    public WorkspaceList Current { get; private set; }

    public bool IsReadOnly => ReadOnlyCause is not null;

    public WorkspaceReadOnlyCause? ReadOnlyCause { get; }

    public string? Notice { get; private set; }

    public bool SaveFailed { get; private set; }

    public event EventHandler? Changed;

    public void Update(Func<WorkspaceList, WorkspaceList> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            var next = change(Current);
            if (ReferenceEquals(next, Current))
            {
                return;
            }

            Current = next;
            if (!IsReadOnly)
            {
                SaveFailed = !Save(next);
                Notice = SaveFailed ? SaveFailedNotice : _loadNotice;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes a workspaces file, as the app would. The harnesses use it to start the app with workspaces (17.2).</summary>
    public static void Write(string filePath, WorkspaceList list)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(list);
        PhysicalWorkspaceFiles.Instance.WriteAllText(filePath, Serialize(list));
    }

    private static string Serialize(WorkspaceList list) => JsonSerializer.Serialize(list, WorkspacesJsonContext.Default.WorkspaceList);

    /// <summary>A hand-edited file's leftovers dropped (1.3): see the class's summary.</summary>
    private static WorkspaceList Clean(WorkspaceList read)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var workspaces = new List<Workspace>();
        foreach (var workspace in read.Workspaces ?? [])
        {
            if (workspace is null || string.IsNullOrWhiteSpace(workspace.Id) || string.IsNullOrWhiteSpace(workspace.Name) || !seen.Add(workspace.Id))
            {
                continue;
            }

            var repositories = new List<WorkspaceRepository>();
            foreach (var repository in workspace.Repositories ?? [])
            {
                if (repository is not null && !string.IsNullOrWhiteSpace(repository.Path)
                    && !repositories.Exists(held => WorkspacePaths.Same(held.Path, repository.Path)))
                {
                    repositories.Add(repository);
                }
            }

            workspaces.Add(workspace with { Name = workspace.Name.Trim(), Repositories = repositories });
        }

        return read with
        {
            // A missing version reads as 0 (the generated serializer sets every property it has): version 1.
            Version = read.Version <= 0 ? WorkspaceList.CurrentVersion : read.Version,
            Workspaces = workspaces,
        };
    }

    private (WorkspaceList List, WorkspaceReadOnlyCause? ReadOnly, string? Notice) Load()
    {
        string json;
        try
        {
            if (!_files.Exists(_filePath))
            {
                return (new WorkspaceList(), null, null);
            }

            json = ReadWithRetries();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Gone between the look and the read: as no file.
            return (new WorkspaceList(), null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Unreadable(ex);
        }

        WorkspaceList list;
        try
        {
            list = Clean(JsonSerializer.Deserialize(json, WorkspacesJsonContext.Default.WorkspaceList) ?? new WorkspaceList());
        }
        catch (JsonException ex)
        {
            // A newer version may have changed what this one reads (D203): its file is kept as it is,
            // read-only, never set aside; only a file of this version or older, or no JSON, is unreadable.
            if (VersionOf(json) is { } version && version > WorkspaceList.CurrentVersion)
            {
                _log.Warning(
                    $"The workspaces file {_filePath} was made by a newer version of VisualCommit (version {version}) and this version cannot read it; " +
                    "the app starts without workspaces, the file is kept as it is, and workspaces cannot be changed.",
                    ex);
                return (new WorkspaceList(), WorkspaceReadOnlyCause.NewerVersion, NewerVersionNotice);
            }

            return Unreadable(ex);
        }

        if (list.Version > WorkspaceList.CurrentVersion)
        {
            _log.Warning(
                $"The workspaces file {_filePath} was made by a newer version of VisualCommit (version {list.Version}); " +
                "what this version understands is read, and workspaces cannot be changed.");
            return (list, WorkspaceReadOnlyCause.NewerVersion, NewerVersionNotice);
        }

        return (list, null, null);
    }

    /// <summary>
    /// The top-level <c>"version"</c> of <paramref name="json"/> when it is JSON whose root is an
    /// object with a whole number there, read as the store reads the file (comments skipped,
    /// trailing commas allowed); null otherwise.
    /// </summary>
    private static int? VersionOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var number)
                ? number
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string ReadWithRetries()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return _files.ReadAllText(_filePath);
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                Thread.Sleep(ReadRetryDelay);
            }
        }
    }

    /// <summary>The file could not be read: set aside under the first free name, or, when that fails too, left alone and never written over.</summary>
    private (WorkspaceList List, WorkspaceReadOnlyCause? ReadOnly, string? Notice) Unreadable(Exception failure)
    {
        try
        {
            var aside = FreeSetAsideName();
            _files.Move(_filePath, aside, overwrite: false);
            var name = Path.GetFileName(aside);
            _log.Warning($"The workspaces file {_filePath} could not be read. It was set aside as {name}, and the app starts without workspaces.", failure);
            return (new WorkspaceList(), null, SetAsideNotice(name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _log.Warning(
                $"The workspaces file {_filePath} could not be read, nor set aside ({ex.Message}). " +
                "It is left as it is, and changes to workspaces are not saved this session.",
                failure);
            return (new WorkspaceList(), WorkspaceReadOnlyCause.Unreadable, UnreadableNotice);
        }
    }

    /// <summary><c>workspaces.unreadable.json</c>, else the first of <c>workspaces.unreadable-2.json</c>, <c>-3</c>, … that does not exist.</summary>
    private string FreeSetAsideName()
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(_filePath))!;
        var stem = Path.GetFileNameWithoutExtension(_filePath);
        var extension = Path.GetExtension(_filePath);
        for (var number = 1; ; number++)
        {
            var name = number == 1 ? $"{stem}.unreadable{extension}" : $"{stem}.unreadable-{number}{extension}";
            var candidate = Path.Combine(folder, name);
            if (!_files.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Writes the whole list beside the file, flushed to the disk, and swaps it in, so that a crash mid-write cannot leave half a file (<see cref="DurableFile"/>). False when it failed (logged).</summary>
    private bool Save(WorkspaceList list)
    {
        try
        {
            var temporary = DurableFile.TemporaryOf(_filePath);
            _files.WriteAllText(temporary, Serialize(list));
            _files.Move(temporary, _filePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"The workspaces could not be saved to {_filePath}; changes to workspaces are lost when VisualCommit closes unless a later save succeeds.", ex);
            return false;
        }
    }
}

/// <summary>
/// How <see cref="JsonWorkspaceStore"/> reaches its file (docs/phase-6-design.md 1.3), so that tests
/// can make a read, a move or a save fail: the operating system's locks on a file are advisory on
/// Linux and macOS, so a test that locks the file would need a branch per platform (0.8).
/// </summary>
public interface IWorkspaceFiles
{
    /// <summary>Whether the file exists.</summary>
    bool Exists(string path);

    /// <summary>The file's text; throws as <see cref="File.ReadAllText(string)"/> does.</summary>
    string ReadAllText(string path);

    /// <summary>Writes the file, its folder made first, flushed to the disk (<see cref="DurableFile.Write"/>); throws as <see cref="File.WriteAllText(string, string?)"/> does.</summary>
    void WriteAllText(string path, string contents);

    /// <summary>Moves (renames) a file; throws as <see cref="File.Move(string, string, bool)"/> does.</summary>
    void Move(string source, string destination, bool overwrite);
}

/// <summary>The disk, for <see cref="JsonWorkspaceStore"/>.</summary>
public sealed class PhysicalWorkspaceFiles : IWorkspaceFiles
{
    public static PhysicalWorkspaceFiles Instance { get; } = new();

    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        DurableFile.Write(path, contents);
    }

    public void Move(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(WorkspaceList))]
internal sealed partial class WorkspacesJsonContext : JsonSerializerContext;
