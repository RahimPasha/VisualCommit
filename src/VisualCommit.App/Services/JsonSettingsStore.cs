using System.Text.Json;
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
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning($"The settings file {_filePath} could not be read. Default settings are used.", ex);
            SetAside();
            return new AppSettings();
        }
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
