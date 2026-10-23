using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;

namespace VisualCommit.App.Tests;

/// <summary>The toolbar's actions, recorded by name, for the tests of the window's keys (docs/phase-6-design.md 6.12).</summary>
internal sealed class RecordingToolbar : IToolbarActions
{
    public List<string> Calls { get; } = [];

    public Task FetchAllAsync() => Record("fetch");

    public Task PullAsync() => Record("pull");

    public Task PushAsync() => Record("push");

    public Task CreateBranchAtHeadAsync() => Record("branch");

    public Task StashAsync() => Record("stash");

    public Task PopAsync() => Record("pop");

    public Task UndoAsync() => Record("undo");

    public Task RedoAsync() => Record("redo");

    private Task Record(string call)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }
}

/// <summary>What the window does for a key that moves the focus (6.3), recorded: in place of the window's own.</summary>
internal sealed class RecordingWindowActions : IWindowKeyActions
{
    public List<string> Calls { get; } = [];

    public void FocusCommitMessage() => Calls.Add("commit message");

    public void FocusFilter() => Calls.Add("filter");

    public void MoveFocusToRegion(bool backwards) => Calls.Add(backwards ? "previous region" : "next region");
}
