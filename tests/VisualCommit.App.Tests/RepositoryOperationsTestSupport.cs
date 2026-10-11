using Avalonia.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the test classes of <see cref="RepositoryOperations"/> share
/// (<see cref="RepositoryOperationsTests"/> and the classes of the WP7 sub-packages beside it).
/// They were one partial class, whose tests xunit ran one after another; as classes of their own
/// they run side by side, and reach these helpers with
/// <c>using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;</c>.
/// </summary>
internal static class RepositoryOperationsTestSupport
{
    /// <summary>The time stamp in a backup ref's name, as a regular expression.</summary>
    public const string BackupTime = "[0-9]{8}T[0-9]{9}Z";

    /// <summary>How long a test waits for a network operation, or for a condition (<see cref="WaitUntilAsync"/>).</summary>
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How many times the queue ran the operation <paramref name="title"/> to its end (its log line, 1.7).</summary>
    public static int Ran(OperationsHarness app, string title) =>
        app.Log.Messages.Count(message => message.Contains($": {title}: ", StringComparison.Ordinal) && message.Contains(" after ", StringComparison.Ordinal));

    /// <summary>The operation <paramref name="title"/> ran exactly once (the queue logs each operation it ran).</summary>
    public static void AssertRanOnce(OperationsHarness app, string title) =>
        Assert.Single(app.Log.Messages, message => message.Contains($": {title}: ", StringComparison.Ordinal) && message.Contains(" after ", StringComparison.Ordinal));

    /// <summary>
    /// Queues an operation of the test's own that waits until the returned source is set, then
    /// runs <paramref name="then"/> (a change to the repository, as an operation ahead of the one
    /// under test would make it); waits until it runs.
    /// </summary>
    public static (TaskCompletionSource Hold, Task<OperationResult> Ahead) HoldQueue(OperationsHarness app, Func<Task>? then = null)
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ahead = app.Repository.Queue.RunAsync(new GitOperation("Held", "Holding…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await hold.Task;
            if (then is not null)
            {
                await then();
            }

            return OperationReport.Done("Held");
        }));
        Assert.NotNull(app.Repository.Queue.Current);
        return (hold, ahead);
    }

    /// <summary>What <paramref name="remote"/> (a folder remote) has at <paramref name="fullRef"/>, unpeeled; null when it has nothing there.</summary>
    public static async Task<string?> RemoteHeadAsync(TempRepo repo, string fullRef, string remote = "origin")
    {
        var output = (await repo.GitAsync("ls-remote", remote, fullRef)).StandardOutput;
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .FirstOrDefault(fields => fields.Length == 2 && fields[1].Trim() == fullRef)?[0];
    }

    /// <summary>A setting's value, or null when it is not set.</summary>
    public static async Task<string?> ConfigAsync(TempRepo repo, string key)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get", key);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>Every text the status bar shows from now on, in order (the running texts too).</summary>
    public static List<string> RecordStatusTexts(OperationsHarness app)
    {
        var texts = new List<string>();
        app.Repository.OperationStatus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(OperationStatusViewModel.Text))
            {
                lock (texts)
                {
                    texts.Add(app.Repository.OperationStatus.Text);
                }
            }
        };
        return texts;
    }

    /// <summary>
    /// The error dialog shows the output and the command of the operation's failed git call
    /// <paramref name="command"/>, as the call log recorded it and the queue cleans it (1.6): on
    /// every git version, whatever its words.
    /// </summary>
    public static void AssertOutputOfFailedCall(OperationsHarness app, ErrorDialogViewModel dialog, string command)
    {
        var call = app.Calls.Snapshot().Last(record => record.CommandText == command);
        Assert.False(call.Succeeded);
        Assert.NotEqual(string.Empty, dialog.Output);
        Assert.Equal(GitOutputCleaner.Clean(call.StandardError, call.StandardOutput), dialog.Output);
        Assert.Equal(command, dialog.Command);
    }

    /// <summary>Waits until the active tab's graph has loaded and its queue is idle.</summary>
    public static async Task<RepositoryViewModel> GraphShownAsync(ShellDriver app)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync(repository);
        return repository;
    }

    /// <summary>The text a text block or a text box with this automation id shows (the dialog on top first, then the window).</summary>
    public static string TextOf(ShellDriver app, string automationId) => app.FindByAutomationId(automationId) switch
    {
        TextBlock block => block.Text ?? string.Empty,
        TextBox box => box.Text ?? string.Empty,
        var other => throw new InvalidOperationException($"'{automationId}' is a {other.GetType().Name}."),
    };

    public static async Task<IReadOnlyList<string>> PorcelainAsync(TempRepo repo) =>
        (await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToList();

    public static string Read(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    public static async Task<string> ShowAsync(TempRepo repo, string commit, string path) =>
        (await repo.GitAsync("show", $"{commit}:{path}")).StandardOutput;

    /// <summary>Waits, checking every 10 ms, until <paramref name="condition"/> holds; fails after <see cref="NetworkTimeout"/>.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + NetworkTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
