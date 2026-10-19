using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="WorkspacePaths"/> (docs/phase-6-design.md 1.2): a fully qualified path normalised
/// as <c>Path.GetFullPath</c> and without a trailing separator, any other path as it is, and one
/// comparison of folders, ignoring case on Windows and macOS and exact elsewhere, as
/// <c>WorktreeKey.Comparer</c> compares.
/// </summary>
public class WorkspacePathsTests
{
    /// <summary>A fully qualified folder on this platform: <c>C:\</c> on Windows, <c>/</c> elsewhere.</summary>
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\" : "/";

    private static readonly char Separator = Path.DirectorySeparatorChar;

    private static bool IgnoresCase => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    [Fact]
    public void A_fully_qualified_path_loses_its_trailing_separator_and_its_dot_segments()
    {
        var expected = Root + "src" + Separator + "api";

        Assert.Equal(expected, WorkspacePaths.Normalise(expected));
        Assert.Equal(expected, WorkspacePaths.Normalise(expected + Separator));
        Assert.Equal(expected, WorkspacePaths.Normalise(Root + "src" + Separator + "." + Separator + "web" + Separator + ".." + Separator + "api"));

        // The root keeps its separator: without it, C: is no longer the root.
        Assert.Equal(Root, WorkspacePaths.Normalise(Root));
    }

    [Fact]
    public void On_Windows_forward_slashes_become_backslashes_elsewhere_they_already_are_the_separator()
    {
        var written = Root + "src/api/";

        Assert.Equal(Root + "src" + Separator + "api", WorkspacePaths.Normalise(written));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("src/api/")]
    [InlineData("../api")]
    [InlineData("")]
    public void A_path_that_is_not_fully_qualified_stays_as_it_is(string path)
    {
        Assert.Equal(path, WorkspacePaths.Normalise(path));
    }

    [Fact]
    public void A_path_with_a_drive_but_no_root_is_not_fully_qualified_on_Windows()
    {
        // "C:src" is relative to the drive's current folder on Windows, a name like any other elsewhere.
        Assert.Equal("C:src", WorkspacePaths.Normalise("C:src"));
    }

    [Fact]
    public void A_path_GetFullPath_refuses_stays_as_it_is()
    {
        var withNull = Root + "src" + Separator + "a\0b";

        Assert.Equal(withNull, WorkspacePaths.Normalise(withNull));
    }

    [Fact]
    public void Same_compares_the_normalised_paths()
    {
        var folder = Root + "src" + Separator + "api";

        Assert.True(WorkspacePaths.Same(folder, folder + Separator));
        Assert.True(WorkspacePaths.Same(folder, Root + "src" + Separator + "." + Separator + "api"));
        Assert.False(WorkspacePaths.Same(folder, Root + "src" + Separator + "web"));
        Assert.False(WorkspacePaths.Same(folder, folder + Separator + "sub"));
        Assert.True(WorkspacePaths.Same("api", "api"));
        Assert.False(WorkspacePaths.Same("api", folder));
    }

    [Fact]
    public void Same_ignores_case_on_Windows_and_macOS_and_not_elsewhere()
    {
        var folder = Root + "src" + Separator + "Api";
        var capitals = Root + "SRC" + Separator + "API";

        Assert.Equal(IgnoresCase, WorkspacePaths.Same(folder, capitals));
        Assert.Equal(IgnoresCase, WorkspacePaths.Same("api", "API"));
    }

    [Fact]
    public void Same_does_not_see_through_a_link_a_path_through_one_is_another_folder_to_it()
    {
        // Git sees through it (1.2's rule 1, OpenedAtTopAsync); a comparison of texts cannot:
        // macOS's /var is a symbolic link to /private/var.
        Assert.False(WorkspacePaths.Same("/var/folders/x/repo", "/private/var/folders/x/repo"));
        Assert.False(WorkspacePaths.Same(Root + "link-to-src" + Separator + "api", Root + "src" + Separator + "api"));
    }

    [Fact]
    public void On_Windows_the_short_name_of_a_folder_that_exists_is_its_long_name_to_Same()
    {
        // Path.GetFullPath expands a path with a "~" in it on Windows, as far as its folders
        // exist: CI's C:\Users\RUNNER~1\… matches what git reports. Elsewhere "~1" is part of a name.
        using var folder = new VisualCommit.Testing.TempDirectory("paths");
        var longName = Directory.CreateDirectory(folder.Combine("A Rather Long Folder Name")).FullName;
        if (!OperatingSystem.IsWindows())
        {
            var named = folder.Combine("ARATHE~1");
            Assert.Equal(named, WorkspacePaths.Normalise(named));
            Assert.False(WorkspacePaths.Same(named, longName));
            return;
        }

        var shortName = folder.Combine(GitRepositoryOpenTests.ShortNameOf(folder.Path, "A Rather Long Folder Name"));

        Assert.Equal(longName, WorkspacePaths.Normalise(shortName), StringComparer.OrdinalIgnoreCase);
        Assert.True(WorkspacePaths.Same(shortName, longName));
        Assert.True(WorkspacePaths.Same(Path.Combine(shortName, "not-made-yet"), Path.Combine(longName, "not-made-yet")));
    }
}
