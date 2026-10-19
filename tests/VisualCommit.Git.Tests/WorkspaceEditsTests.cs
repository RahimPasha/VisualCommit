using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="WorkspaceEdits"/> (docs/phase-6-design.md 1.4): each edit, and each one that changes
/// nothing giving back the very same list (<see cref="Assert.Same"/>), so that the store neither
/// saves nor tells anyone.
/// </summary>
public class WorkspaceEditsTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\src\" : "/src/";

    private static readonly string Api = Root + "api";

    private static readonly string Web = Root + "web";

    private static readonly string Docs = Root + "docs";

    private static readonly WorkspaceList Two = new()
    {
        Workspaces =
        [
            new Workspace("w1", "Platform", [new WorkspaceRepository(Api), new WorkspaceRepository(Web), new WorkspaceRepository(Docs)]),
            new Workspace("w2", "Mobile", []),
        ],
    };

    private static IEnumerable<string> PathsOf(WorkspaceList list, string id) => list.Find(id)!.Repositories.Select(repository => repository.Path);

    [Fact]
    public void Create_appends_an_empty_workspace_with_its_name_trimmed()
    {
        var created = WorkspaceEdits.Create(Two, "w3", "  Web  ");

        Assert.Equal(["w1", "w2", "w3"], created.Workspaces.Select(workspace => workspace.Id));
        Assert.Equal("Web", created.Workspaces[2].Name);
        Assert.Empty(created.Workspaces[2].Repositories);
        Assert.Same(Two.Workspaces[0], created.Workspaces[0]);
        Assert.Equal(2, Two.Workspaces.Count);
    }

    [Fact]
    public void Create_with_an_id_already_held_changes_nothing()
    {
        Assert.Same(Two, WorkspaceEdits.Create(Two, "w1", "Again"));
    }

    [Fact]
    public void Rename_changes_the_name_alone_and_a_change_of_case_counts()
    {
        var renamed = WorkspaceEdits.Rename(Two, "w1", " Backend ");
        var recased = WorkspaceEdits.Rename(Two, "w1", "PLATFORM");

        Assert.Equal(["Backend", "Mobile"], renamed.Workspaces.Select(workspace => workspace.Name));
        Assert.Equal(Two.Workspaces[0].Repositories, renamed.Workspaces[0].Repositories);
        Assert.Equal("w1", renamed.Workspaces[0].Id);
        Assert.Equal("PLATFORM", recased.Workspaces[0].Name);
    }

    [Fact]
    public void Rename_to_the_same_name_or_of_an_unknown_id_changes_nothing()
    {
        Assert.Same(Two, WorkspaceEdits.Rename(Two, "w1", "Platform"));
        Assert.Same(Two, WorkspaceEdits.Rename(Two, "w1", "  Platform "));
        Assert.Same(Two, WorkspaceEdits.Rename(Two, "w9", "Other"));
    }

    [Fact]
    public void Delete_removes_the_workspace_and_an_unknown_id_changes_nothing()
    {
        var deleted = WorkspaceEdits.Delete(Two, "w1");

        Assert.Equal(["w2"], deleted.Workspaces.Select(workspace => workspace.Id));
        Assert.Same(Two, WorkspaceEdits.Delete(Two, "w9"));
    }

    [Fact]
    public void Append_adds_in_the_order_given_each_path_not_held_yet()
    {
        var mobile = Root + "mobile";
        var billing = Root + "billing";

        var appended = WorkspaceEdits.Append(Two, "w1", [mobile, Api + Path.DirectorySeparatorChar, billing, mobile, " "]);

        Assert.Equal([Api, Web, Docs, mobile, billing], PathsOf(appended, "w1"));
        Assert.Same(Two.Workspaces[1], appended.Workspaces[1]);
    }

    [Fact]
    public void Append_keeps_a_path_as_it_was_given()
    {
        var spelled = Root + "mobile" + Path.DirectorySeparatorChar;

        Assert.Equal(spelled, PathsOf(WorkspaceEdits.Append(Two, "w2", [spelled]), "w2").Single());
    }

    [Fact]
    public void Append_of_paths_all_held_or_to_an_unknown_id_changes_nothing()
    {
        Assert.Same(Two, WorkspaceEdits.Append(Two, "w1", [Web, Api]));
        Assert.Same(Two, WorkspaceEdits.Append(Two, "w1", []));
        Assert.Same(Two, WorkspaceEdits.Append(Two, "w9", [Root + "mobile"]));
    }

    [Fact]
    public void Append_matches_held_paths_ignoring_case_where_the_platform_does()
    {
        var capitals = Api.ToUpperInvariant();

        var appended = WorkspaceEdits.Append(Two, "w1", [capitals]);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Same(Two, appended);
        }
        else
        {
            Assert.Equal([Api, Web, Docs, capitals], PathsOf(appended, "w1"));
        }
    }

    [Fact]
    public void Remove_takes_the_path_out_matched_as_the_same_folder()
    {
        var removed = WorkspaceEdits.Remove(Two, "w1", Web + Path.DirectorySeparatorChar);

        Assert.Equal([Api, Docs], PathsOf(removed, "w1"));
        Assert.Same(Two, WorkspaceEdits.Remove(Two, "w1", Root + "mobile"));
        Assert.Same(Two, WorkspaceEdits.Remove(Two, "w9", Web));
    }

    [Fact]
    public void Move_swaps_with_the_neighbour_and_does_nothing_at_either_end()
    {
        Assert.Equal([Web, Api, Docs], PathsOf(WorkspaceEdits.Move(Two, "w1", Web, -1), "w1"));
        Assert.Equal([Api, Docs, Web], PathsOf(WorkspaceEdits.Move(Two, "w1", Web, +1), "w1"));
        Assert.Same(Two, WorkspaceEdits.Move(Two, "w1", Api, -1));
        Assert.Same(Two, WorkspaceEdits.Move(Two, "w1", Docs, +1));
        Assert.Same(Two, WorkspaceEdits.Move(Two, "w1", Root + "mobile", +1));
        Assert.Same(Two, WorkspaceEdits.Move(Two, "w9", Web, +1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    public void Move_by_anything_but_one_place_is_refused(int delta)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceEdits.Move(Two, "w1", Web, delta));
    }

    [Fact]
    public void Replace_puts_the_new_path_in_the_old_ones_place()
    {
        var located = Root + "moved" + Path.DirectorySeparatorChar + "web";

        var replaced = WorkspaceEdits.Replace(Two, "w1", Web, located);

        Assert.Equal([Api, located, Docs], PathsOf(replaced, "w1"));
    }

    [Fact]
    public void Replace_by_a_path_held_already_or_of_a_path_not_held_changes_nothing()
    {
        Assert.Same(Two, WorkspaceEdits.Replace(Two, "w1", Web, Api));
        Assert.Same(Two, WorkspaceEdits.Replace(Two, "w1", Web, Web + Path.DirectorySeparatorChar));
        Assert.Same(Two, WorkspaceEdits.Replace(Two, "w1", Root + "mobile", Root + "other"));
        Assert.Same(Two, WorkspaceEdits.Replace(Two, "w9", Web, Root + "other"));
    }

    [Fact]
    public void A_repository_moved_is_the_same_object_so_its_other_fields_survive()
    {
        // A later version may add fields to WorkspaceRepository (1.1); an edit copies the object.
        var list = new WorkspaceList { Workspaces = [new Workspace("w1", "One", [new WorkspaceRepository(Api), new WorkspaceRepository(Web)])] };

        var moved = WorkspaceEdits.Move(list, "w1", Web, -1);

        Assert.Same(list.Workspaces[0].Repositories[1], moved.Workspaces[0].Repositories[0]);
        Assert.Equal(list.Version, moved.Version);
    }
}
