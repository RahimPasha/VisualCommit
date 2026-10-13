using System.Diagnostics;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The walk-throughs' picture folders, emptied once per run of the processes that fill them
/// (<see cref="VisualRun.StartRun"/>, docs/phase-5-design.md 15.3): phase 5's two projects save
/// into one folder and must not empty each other's pictures. On folders of a temporary folder,
/// never the real artifacts/visual/.
/// </summary>
public class VisualRunTests
{
    [Fact]
    public void A_run_empties_the_folder_of_its_pictures_once_and_a_later_run_again()
    {
        using var temp = new TempDirectory("visual run");
        var folder = temp.Combine("phase-5", "scripted");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a1a-old.png"), "an earlier run's picture");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a picture");

        Assert.True(VisualRun.StartRun(folder, "run 1"));
        Assert.False(File.Exists(Path.Combine(folder, "a1a-old.png")));
        Assert.True(File.Exists(Path.Combine(folder, "notes.txt")));
        Assert.Equal("run 1", File.ReadAllText(Path.Combine(folder, VisualRun.RunFile)));

        // The run's other process (phase 5's other project) keeps what the first saved.
        File.WriteAllText(Path.Combine(folder, "a1a-toolbar.png"), "this run's picture");
        Assert.False(VisualRun.StartRun(folder, "run 1"));
        Assert.True(File.Exists(Path.Combine(folder, "a1a-toolbar.png")));

        // The next run starts afresh.
        Assert.True(VisualRun.StartRun(folder, "run 2"));
        Assert.False(File.Exists(Path.Combine(folder, "a1a-toolbar.png")));
        Assert.Equal("run 2", File.ReadAllText(Path.Combine(folder, VisualRun.RunFile)));
    }

    [Fact]
    public void A_folder_that_does_not_exist_yet_is_made_for_the_run()
    {
        using var temp = new TempDirectory("visual run");
        var folder = temp.Combine("artifacts", "visual", "phase-5", "scripted");

        Assert.True(VisualRun.StartRun(folder, "run 1"));
        Assert.Equal("run 1", File.ReadAllText(Path.Combine(folder, VisualRun.RunFile)));
    }

    [Fact]
    public async Task Of_the_processes_of_one_run_starting_at_once_exactly_one_empties_the_folder()
    {
        using var temp = new TempDirectory("visual run");
        var folder = temp.Combine("scripted");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "u16a-old.png"), "an earlier run's picture");

        var started = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => VisualRun.StartRun(folder, "one run"), TestContext.Current.CancellationToken)));

        Assert.Equal(1, started.Count(emptied => emptied));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.png"));
    }

    /// <summary>
    /// Under <c>dotnet test</c> (the default run, CI) the parent is the dotnet CLI, which started
    /// every test project of the run; run on its own, from a shell or an IDE, the test program is
    /// a run of its own.
    /// </summary>
    [Fact]
    public void The_run_is_the_dotnet_process_that_started_this_one_and_otherwise_this_process()
    {
        using var self = Process.GetCurrentProcess();
        var parentId = VisualRun.ParentProcessId();
        Assert.NotNull(parentId);
        Assert.NotEqual(Environment.ProcessId, parentId);
        using var parent = Process.GetProcessById(parentId.Value);

        if (string.Equals(parent.ProcessName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(VisualRun.KeyOf(parent), VisualRun.RunKey);
            Assert.True(parent.StartTime <= self.StartTime, "The run's process started after this one.");
        }
        else
        {
            Assert.Equal(VisualRun.KeyOf(self), VisualRun.RunKey);
        }

        Assert.Equal(VisualRun.RunKey, VisualRun.RunKey);
    }
}
