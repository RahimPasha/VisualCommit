using VisualCommit.App.Services;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Without_a_file_the_defaults_apply_and_no_file_is_written()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");

        var store = new JsonSettingsStore(file);

        Assert.Equal(new AppSettings(), store.Current);
        Assert.Equal(AppTheme.Dark, store.Current.Theme);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void A_change_is_saved_and_a_new_store_loads_it()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");

        new JsonSettingsStore(file).Update(settings => settings with { Theme = AppTheme.Light });
        var reloaded = new JsonSettingsStore(file);

        Assert.Equal(AppTheme.Light, reloaded.Current.Theme);
    }

    [Fact]
    public void The_file_is_readable_json_with_named_values()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");

        new JsonSettingsStore(file).Update(settings => settings with { Theme = AppTheme.Light });

        var json = File.ReadAllText(file);
        Assert.Contains("\"theme\": \"Light\"", json);
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public void The_data_folder_is_created_on_the_first_save()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("not", "yet", "there", "settings.json");

        new JsonSettingsStore(file).Update(settings => settings with { Theme = AppTheme.Light });

        Assert.True(File.Exists(file));
    }

    [Fact]
    public void A_change_that_changes_nothing_does_not_write_the_file()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");
        var store = new JsonSettingsStore(file);

        store.Update(settings => settings with { Theme = AppTheme.Dark });

        Assert.False(File.Exists(file));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"theme\": \"Purple\" }")]
    public void An_unreadable_file_is_set_aside_and_the_defaults_apply(string content)
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");
        File.WriteAllText(file, content);
        var log = new RecordingLog();

        var store = new JsonSettingsStore(file, log);

        Assert.Equal(new AppSettings(), store.Current);
        Assert.False(File.Exists(file));
        Assert.Equal(content, File.ReadAllText(folder.Combine("settings.unreadable.json")));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not be read"));

        // The store still works afterwards.
        store.Update(settings => settings with { Theme = AppTheme.Light });
        Assert.Equal(AppTheme.Light, new JsonSettingsStore(file).Current.Theme);
    }

    [Fact]
    public void Settings_this_version_does_not_know_are_ignored_and_missing_ones_get_defaults()
    {
        using var folder = new TempDirectory("settings");
        var file = folder.Combine("settings.json");
        File.WriteAllText(file, "{ \"somethingFromTheFuture\": 42, // with a comment\n \"another\": { \"nested\": true }, }");

        var store = new JsonSettingsStore(file);

        Assert.Equal(AppTheme.Dark, store.Current.Theme);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void A_save_that_fails_is_logged_and_the_change_still_applies_in_memory()
    {
        using var folder = new TempDirectory("settings");

        // A folder where the file should be makes every write fail.
        var file = folder.Combine("settings.json");
        Directory.CreateDirectory(file);
        var log = new RecordingLog();
        var store = new JsonSettingsStore(file, log);

        store.Update(settings => settings with { Theme = AppTheme.Light });

        Assert.Equal(AppTheme.Light, store.Current.Theme);
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be saved"));
    }
}
