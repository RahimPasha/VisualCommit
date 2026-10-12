using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>
/// Keeps the settings in a JSON file. A missing file means default settings. A file that cannot
/// be read is set aside as <c>settings.unreadable.json</c> and the defaults are used, so a damaged
/// file never stops the app from starting.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly Lock _gate = new();
    private readonly string _filePath;
    private readonly IAppLog _log;

    public JsonSettingsStore(string filePath, IAppLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _log = log ?? NullAppLog.Instance;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Update(Func<AppSettings, AppSettings> change)
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

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return Read(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning($"The settings file {_filePath} could not be read. Default settings are used.", ex);
            SetAside();
            return new AppSettings();
        }
    }

    /// <summary>
    /// The settings in <paramref name="json"/>, with the defaults of <see cref="AppSettings"/> for
    /// the settings the file does not have. The generated serializer sets every init-only
    /// property, so a setting missing from the file would get 0 or false instead of its default
    /// (a phase 2 file has no <c>autoFetchMinutes</c>, whose default is 5): the file's values are
    /// laid over the defaults' JSON first.
    /// </summary>
    private static AppSettings Read(string json)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        if (JsonNode.Parse(json, documentOptions: options) is not JsonObject values)
        {
            // Not an object: read as before, which refuses it or gives the defaults for "null".
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }

        var merged = JsonSerializer.SerializeToNode(new AppSettings(), SettingsJsonContext.Default.AppSettings)!.AsObject();
        foreach (var (name, value) in values.ToList())
        {
            merged[name] = value?.DeepClone();
        }

        return merged.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
    }

    private void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Write beside the file and swap it in, so a crash mid-write cannot leave half a file.
            var temporary = _filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
            File.Move(temporary, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"The settings could not be saved to {_filePath}.", ex);
        }
    }

    private void SetAside()
    {
        try
        {
            var aside = Path.Combine(Path.GetDirectoryName(_filePath)!, "settings.unreadable.json");
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
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
