using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="JsonWorkspaceStore"/> (docs/phase-6-design.md 1.3, D189, D203): every case of the
/// file, on the disk; a read, a set-aside or a save that fails through a fake
/// <see cref="IWorkspaceFiles"/> over the disk, never through the operating system's locks, which
/// are advisory on Linux and macOS (0.8).
/// </summary>
public class JsonWorkspaceStoreTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"D:\src\" : "/src/";

    private static readonly WorkspaceList Platform = new()
    {
        Workspaces =
        [
            new Workspace("3f2c", "Platform", [new WorkspaceRepository(Root + "api"), new WorkspaceRepository(Root + "web")]),
            new Workspace("9a1b", "Mobile", []),
        ],
    };

    [Fact]
    public void Without_a_file_the_list_is_empty_and_the_first_change_writes_the_file()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);

        var store = new JsonWorkspaceStore(file);

        Assert.Empty(store.Current.Workspaces);
        Assert.Equal(WorkspaceList.CurrentVersion, store.Current.Version);
        Assert.Equal((false, null, null, false), (store.IsReadOnly, store.ReadOnlyCause, store.Notice, store.SaveFailed));
        Assert.False(File.Exists(file));

        store.Update(list => WorkspaceEdits.Create(list, "3f2c", "Platform"));

        Assert.True(File.Exists(file));
        Assert.False(File.Exists(file + ".tmp"));
        Assert.Equal(["Platform"], new JsonWorkspaceStore(file).Current.Workspaces.Select(workspace => workspace.Name));
    }

    [Fact]
    public void What_is_saved_is_read_back_in_order_as_indented_camel_case_json()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);

        new JsonWorkspaceStore(file).Update(_ => Platform);

        var read = new JsonWorkspaceStore(file).Current;
        Assert.Equal(1, read.Version);
        Assert.Equal(
            [("3f2c", "Platform", Root + "api|" + Root + "web"), ("9a1b", "Mobile", string.Empty)],
            read.Workspaces.Select(Described));
        var text = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.StartsWith("{\n  \"version\": 1,\n  \"workspaces\": [", text, StringComparison.Ordinal);
        Assert.Contains("\"repositories\": [", text, StringComparison.Ordinal);
        Assert.Contains("\"path\": ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_and_the_harness_write_a_file_the_store_reads()
    {
        using var data = new TempDirectory("data");

        JsonWorkspaceStore.Write(Path.Combine(data.Path, "nested", "workspaces.json"), Platform);
        ShellDriver.WriteWorkspaces(data.Path, Platform);

        Assert.Equal(["Platform", "Mobile"], new JsonWorkspaceStore(Path.Combine(data.Path, "nested", "workspaces.json")).Current.Workspaces.Select(workspace => workspace.Name));
        Assert.Equal(["Platform", "Mobile"], new JsonWorkspaceStore(new AppPaths(data.Path).WorkspacesFile).Current.Workspaces.Select(workspace => workspace.Name));
    }

    [Fact]
    public void Bad_json_is_set_aside_with_a_notice_and_the_list_is_empty()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        File.WriteAllText(file, "{ this is not json");
        var log = new RecordingLog();

        var store = new JsonWorkspaceStore(file, log);

        Assert.Empty(store.Current.Workspaces);
        Assert.False(store.IsReadOnly);
        Assert.Equal("workspaces.json could not be read, so it was set aside as workspaces.unreadable.json.", store.Notice);
        Assert.False(File.Exists(file));
        Assert.Equal("{ this is not json", File.ReadAllText(Path.Combine(data.Path, "workspaces.unreadable.json")));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not be read", StringComparison.Ordinal));

        // The next change writes a new file; the notice stays for the session.
        store.Update(list => WorkspaceEdits.Create(list, "w1", "Web"));
        Assert.True(File.Exists(file));
        Assert.Equal(JsonWorkspaceStore.SetAsideNotice("workspaces.unreadable.json"), store.Notice);
    }

    [Fact]
    public void A_second_set_aside_keeps_the_first_copy_and_takes_the_next_free_name()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        File.WriteAllText(Path.Combine(data.Path, "workspaces.unreadable.json"), "the first copy");
        File.WriteAllText(file, "[ 1, 2");

        var store = new JsonWorkspaceStore(file);

        Assert.Equal("workspaces.json could not be read, so it was set aside as workspaces.unreadable-2.json.", store.Notice);
        Assert.Equal("the first copy", File.ReadAllText(Path.Combine(data.Path, "workspaces.unreadable.json")));
        Assert.Equal("[ 1, 2", File.ReadAllText(Path.Combine(data.Path, "workspaces.unreadable-2.json")));

        File.WriteAllText(file, "nope");
        Assert.Equal(JsonWorkspaceStore.SetAsideNotice("workspaces.unreadable-3.json"), new JsonWorkspaceStore(file).Notice);
    }

    [Fact]
    public void A_file_that_can_be_neither_read_nor_set_aside_makes_the_store_read_only_and_is_never_written_over()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        JsonWorkspaceStore.Write(file, Platform);
        var bytes = File.ReadAllBytes(file);
        var files = new FakeFiles
        {
            FailRead = _ => new IOException("The process cannot access the file because it is being used by another process."),
            FailMove = (_, _) => new IOException("The process cannot access the file because it is being used by another process."),
        };
        var log = new RecordingLog();

        var store = new JsonWorkspaceStore(file, log, files);

        Assert.Equal((true, WorkspaceReadOnlyCause.Unreadable), (store.IsReadOnly, store.ReadOnlyCause));
        Assert.Equal("workspaces.json could not be read, so changes to workspaces are not saved this session.", store.Notice);
        Assert.Empty(store.Current.Workspaces);
        Assert.Equal(3, files.Reads);
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Update(list => WorkspaceEdits.Create(list, "w1", "Web"));

        Assert.Equal(["Web"], store.Current.Workspaces.Select(workspace => workspace.Name));
        Assert.Equal(1, changed);
        Assert.Equal(0, files.Writes);
        Assert.False(store.SaveFailed);
        Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.False(File.Exists(Path.Combine(data.Path, "workspaces.unreadable.json")));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("nor set aside", StringComparison.Ordinal));
    }

    [Fact]
    public void A_read_refused_by_the_files_permissions_is_not_tried_again_and_the_file_is_set_aside()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        JsonWorkspaceStore.Write(file, Platform);
        var files = new FakeFiles { FailRead = _ => new UnauthorizedAccessException("Access to the path is denied.") };

        var store = new JsonWorkspaceStore(file, files: files);

        Assert.Equal(1, files.Reads);
        Assert.Equal(JsonWorkspaceStore.SetAsideNotice("workspaces.unreadable.json"), store.Notice);
        Assert.False(store.IsReadOnly);
        Assert.True(File.Exists(Path.Combine(data.Path, "workspaces.unreadable.json")));
    }

    [Fact]
    public void A_read_that_throws_twice_and_then_succeeds_reads_the_file()
    {
        // A virus scanner or a backup may hold the file a moment (1.3).
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        JsonWorkspaceStore.Write(file, Platform);
        var files = new FakeFiles { FailRead = attempt => attempt <= 2 ? new IOException("In use.") : null };

        var store = new JsonWorkspaceStore(file, files: files);

        Assert.Equal(3, files.Reads);
        Assert.Equal(["Platform", "Mobile"], store.Current.Workspaces.Select(workspace => workspace.Name));
        Assert.Null(store.Notice);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void A_newer_versions_file_is_read_as_far_as_understood_and_never_saved()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        var text = $$"""
            {
              "version": 2,
              "colour": "blue",
              "workspaces": [ { "id": "w1", "name": "Platform", "pinned": true, "repositories": [ { "path": {{System.Text.Json.JsonSerializer.Serialize(Root + "api")}}, "alias": "API" } ] } ]
            }
            """;
        File.WriteAllText(file, text);
        var bytes = File.ReadAllBytes(file);
        var log = new RecordingLog();

        var store = new JsonWorkspaceStore(file, log);

        Assert.Equal((true, WorkspaceReadOnlyCause.NewerVersion), (store.IsReadOnly, store.ReadOnlyCause));
        Assert.Equal("workspaces.json was made by a newer version of VisualCommit, so workspaces cannot be changed in this version.", store.Notice);
        Assert.Equal(2, store.Current.Version);
        Assert.Equal((Root + "api", "Platform"), (store.Current.Workspaces.Single().Repositories.Single().Path, store.Current.Workspaces.Single().Name));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("newer version", StringComparison.Ordinal));
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.Update(list => WorkspaceEdits.Rename(list, "w1", "Renamed"));

        Assert.Equal(1, changed);
        Assert.Equal("Renamed", store.Current.Workspaces.Single().Name);
        Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Theory]
    [InlineData("""{ "workspaces": [] }""")]
    [InlineData("""{ "version": 0, "workspaces": [] }""")]
    public void A_missing_or_zero_version_is_version_1(string text)
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        File.WriteAllText(file, text);

        var store = new JsonWorkspaceStore(file);

        Assert.Equal(1, store.Current.Version);
        Assert.False(store.IsReadOnly);
        Assert.Null(store.Notice);
    }

    [Fact]
    public void A_hand_edited_files_leftovers_are_dropped_names_trimmed_and_gone_folders_kept()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        var api = System.Text.Json.JsonSerializer.Serialize(Root + "api");
        var apiAgain = System.Text.Json.JsonSerializer.Serialize(Root + "api" + Path.DirectorySeparatorChar);
        var gone = System.Text.Json.JsonSerializer.Serialize(Root + "no-such-folder");
        File.WriteAllText(file, $$"""
            {
              "version": 1,
              "workspaces": [
                null,
                { "id": "", "name": "No id", "repositories": [] },
                { "id": "w0", "name": "   ", "repositories": [] },
                { "id": "w1", "name": "  Platform  ", "repositories": [ null, { "path": "" }, { "path": {{api}} }, { "path": {{apiAgain}} }, { "path": {{gone}} } ] },
                { "id": "w1", "name": "Same id", "repositories": [] },
                { "id": "w2", "name": "platform" },
                { "id": "w3", "name": "Mobile", "repositories": null }
              ]
            }
            """);

        var store = new JsonWorkspaceStore(file);

        Assert.Equal(
            [("w1", "Platform", Root + "api|" + Root + "no-such-folder"), ("w2", "platform", string.Empty), ("w3", "Mobile", string.Empty)],
            store.Current.Workspaces.Select(Described));
        Assert.Null(store.Notice);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{ "version": 1, "workspaces": null }""")]
    public void A_null_list_reads_as_empty(string text)
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        File.WriteAllText(file, text);

        var store = new JsonWorkspaceStore(file);

        Assert.Empty(store.Current.Workspaces);
        Assert.Null(store.Notice);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Changed_comes_once_per_update_that_changes_the_list_and_a_change_to_nothing_saves_nothing()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        var files = new FakeFiles();
        var store = new JsonWorkspaceStore(file, files: files);
        var seen = new List<(string? Notice, bool SaveFailed, int Count)>();
        store.Changed += (_, _) => seen.Add((store.Notice, store.SaveFailed, store.Current.Workspaces.Count));

        store.Update(list => WorkspaceEdits.Create(list, "w1", "Web"));
        store.Update(list => WorkspaceEdits.Rename(list, "w1", "Web"));
        store.Update(list => list);
        store.Update(list => WorkspaceEdits.Create(list, "w2", "Mobile"));

        Assert.Equal([(null, false, 1), (null, false, 2)], seen);
        Assert.Equal(2, files.Writes);
    }

    [Fact]
    public void A_failed_save_keeps_the_change_for_the_session_and_says_so_until_a_save_succeeds()
    {
        using var data = new TempDirectory("data");
        var file = WorkspacesFile(data);
        File.WriteAllText(file, "not json");
        var failSave = true;
        var files = new FakeFiles
        {
            FailWrite = path => failSave && path.EndsWith(".tmp", StringComparison.Ordinal) ? new IOException("Disk full.") : null,
        };
        var log = new RecordingLog();
        var store = new JsonWorkspaceStore(file, log, files);
        var setAside = JsonWorkspaceStore.SetAsideNotice("workspaces.unreadable.json");
        Assert.Equal(setAside, store.Notice);
        var seen = new List<(string? Notice, bool SaveFailed)>();
        store.Changed += (_, _) => seen.Add((store.Notice, store.SaveFailed));

        store.Update(list => WorkspaceEdits.Create(list, "w1", "Web"));

        Assert.Equal(["Web"], store.Current.Workspaces.Select(workspace => workspace.Name));
        Assert.True(store.SaveFailed);
        Assert.Equal("workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.", store.Notice);
        Assert.False(store.IsReadOnly);
        Assert.False(File.Exists(file));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be saved", StringComparison.Ordinal));

        // The swap failing (the file held, or read-only) fails the save as well.
        failSave = false;
        files.FailMove = (_, destination) => destination == file ? new UnauthorizedAccessException("Access to the path is denied.") : null;
        store.Update(list => WorkspaceEdits.Create(list, "w2", "Mobile"));
        Assert.True(store.SaveFailed);

        // A save that succeeds writes the whole list and brings back the load's notice.
        files.FailMove = null;
        store.Update(list => WorkspaceEdits.Create(list, "w3", "Docs"));

        Assert.False(store.SaveFailed);
        Assert.Equal(setAside, store.Notice);
        Assert.Equal(["Web", "Mobile", "Docs"], new JsonWorkspaceStore(file).Current.Workspaces.Select(workspace => workspace.Name));
        Assert.Equal(
            [(JsonWorkspaceStore.SaveFailedNotice, true), (JsonWorkspaceStore.SaveFailedNotice, true), (setAside, false)],
            seen);
    }

    private static string WorkspacesFile(TempDirectory data) => new AppPaths(data.Path).WorkspacesFile;

    /// <summary>A workspace as its id, its name and its paths joined by <c>|</c>.</summary>
    private static (string Id, string Name, string Paths) Described(Workspace workspace) =>
        (workspace.Id, workspace.Name, string.Join("|", workspace.Repositories.Select(repository => repository.Path)));

    /// <summary>The disk, with a read, a move or a write that a test makes fail.</summary>
    private sealed class FakeFiles : IWorkspaceFiles
    {
        /// <summary>The exception of read attempt n (from 1), or null to read.</summary>
        public Func<int, Exception?>? FailRead { get; init; }

        public Func<string, string, Exception?>? FailMove { get; set; }

        public Func<string, Exception?>? FailWrite { get; init; }

        public int Reads { get; private set; }

        public int Writes { get; private set; }

        public bool Exists(string path) => File.Exists(path);

        public string ReadAllText(string path)
        {
            Reads++;
            if (FailRead?.Invoke(Reads) is { } failure)
            {
                throw failure;
            }

            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string contents)
        {
            Writes++;
            if (FailWrite?.Invoke(path) is { } failure)
            {
                throw failure;
            }

            PhysicalWorkspaceFiles.Instance.WriteAllText(path, contents);
        }

        public void Move(string source, string destination, bool overwrite)
        {
            if (FailMove?.Invoke(source, destination) is { } failure)
            {
                throw failure;
            }

            File.Move(source, destination, overwrite);
        }
    }
}
