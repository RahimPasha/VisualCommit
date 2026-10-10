using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;

namespace VisualCommit.App.Services;

/// <summary>
/// Keeps the session state (D46) in a JSON file. A missing file means a first start: one empty
/// tab and nothing to restore. A file that cannot be read is set aside as
/// <c>session.unreadable.json</c> and the app starts as on a first start, so a damaged file
/// never stops it.
/// </summary>
public sealed class JsonSessionStore : ISessionStore
{
    private readonly Lock _gate = new();
    private readonly string _filePath;
    private readonly IAppLog _log;

    public JsonSessionStore(string filePath, IAppLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _log = log ?? NullAppLog.Instance;
        Current = Load();
    }

    public SessionState Current { get; private set; }

    public void Update(Func<SessionState, SessionState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            var next = change(Current);
            if (next == Current)
            {
                return;
            }

            Current = next;
            Save(next);
        }
    }

    /// <summary>Writes a session file, as the app would. Tests use it to start the app with repositories open.</summary>
    public static void Write(string filePath, SessionState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(state, SessionJsonContext.Default.SessionState));
    }

    private SessionState Load()
    {
        if (!File.Exists(_filePath))
        {
            return new SessionState();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize(json, SessionJsonContext.Default.SessionState) ?? new SessionState();

            // Lists that a hand-edited file left out or set to null are read as empty, and a tab
            // with a blank folder as an empty tab.
            return state with
            {
                Tabs = state.Tabs?
                    .Where(tab => tab is not null)
                    .Select(tab => string.IsNullOrWhiteSpace(tab.RepositoryPath) ? new TabState(null) : tab)
                    .ToList() ?? [],
                Recent = state.Recent?.Where(recent => recent is not null && !string.IsNullOrWhiteSpace(recent.Path)).ToList() ?? [],
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _log.Warning($"The session file {_filePath} could not be read. The app starts with one empty tab.", ex);
            SetAside();
            return new SessionState();
        }
    }

    private void Save(SessionState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Write beside the file and swap it in, so a crash mid-write cannot leave half a file.
            var temporary = _filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, SessionJsonContext.Default.SessionState));
            File.Move(temporary, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"The session could not be saved to {_filePath}.", ex);
        }
    }

    private void SetAside()
    {
        try
        {
            var aside = Path.Combine(Path.GetDirectoryName(_filePath)!, "session.unreadable.json");
            File.Move(_filePath, aside, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next save overwrites the unreadable file instead.
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SessionState))]
internal sealed partial class SessionJsonContext : JsonSerializerContext;
