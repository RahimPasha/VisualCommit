using Avalonia.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.App.Views.Rebase;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;

namespace VisualCommit.App.Tests;

/// <summary>
/// An <see cref="IInteractiveRebaseHost"/> that records what the list asks of its repository and
/// answers from queues (docs/phase-4-design.md 2.2): Start's plans, the details shown, the
/// questions and message dialogs, closing. With a <see cref="DialogHostViewModel"/> it shows the
/// app's real dialogs instead of answering from the queues, for the view tests.
/// </summary>
internal sealed class RecordingRebaseHost(DialogHostViewModel? dialogs = null) : IInteractiveRebaseHost
{
    /// <summary>The plans Start handed over, in order.</summary>
    public List<RebasePlan> Plans { get; } = [];

    /// <summary>What Start answers, one per call: true (done or stopped, the list closes) unless queued otherwise.</summary>
    public Queue<bool> StartAnswers { get; } = new();

    /// <summary>When set, Start waits for it (the operation "waits or runs") before it answers.</summary>
    public TaskCompletionSource<bool>? HeldStart { get; set; }

    /// <summary>Every commit the list showed in the right panel, in order.</summary>
    public List<string?> Details { get; } = [];

    /// <summary>Every question the list asked.</summary>
    public List<ConfirmationRequest> Questions { get; } = [];

    /// <summary>The questions' answers, one per question (without a dialog host): false ("Keep editing") when none is queued.</summary>
    public Queue<bool> ConfirmAnswers { get; } = new();

    /// <summary>Every message dialog the list opened: its title, the message it was filled with, its confirm button.</summary>
    public List<(string Title, string Message, string ConfirmText)> MessageRequests { get; } = [];

    /// <summary>The message dialogs' answers, one per dialog (without a dialog host): null (Cancel) when none is queued.</summary>
    public Queue<string?> MessageAnswers { get; } = new();

    /// <summary>How many times the list asked to be closed.</summary>
    public int Closed { get; private set; }

    /// <summary>Called with each commit the list shows in the right panel (a test selects it in a real graph).</summary>
    public Action<string?>? OnShowDetails { get; set; }

    /// <summary>Called when the list asks to be closed.</summary>
    public Action? OnClose { get; set; }

    public async Task<bool> StartAsync(RebasePlan plan)
    {
        Plans.Add(plan);
        if (HeldStart is { } held)
        {
            return await held.Task;
        }

        return StartAnswers.Count == 0 || StartAnswers.Dequeue();
    }

    public void ShowDetails(string? sha)
    {
        Details.Add(sha);
        OnShowDetails?.Invoke(sha);
    }

    public Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        Questions.Add(request);
        return dialogs is not null ? dialogs.ConfirmAsync(request) : Task.FromResult(ConfirmAnswers.Count > 0 && ConfirmAnswers.Dequeue());
    }

    public Task<string?> AskMessageAsync(string title, string message, string confirmText)
    {
        MessageRequests.Add((title, message, confirmText));
        return dialogs is not null
            ? dialogs.ShowAsync(new MessageDialogViewModel(title, message, confirmText))
            : Task.FromResult(MessageAnswers.Count > 0 ? MessageAnswers.Dequeue() : null);
    }

    public void Close()
    {
        Closed++;
        OnClose?.Invoke();
    }
}

/// <summary>
/// The "interactive" scenario's list as the git layer reads it for "Interactive rebase of main from
/// here…" on Base (docs/test-reports/phase-4.md, part B: the SHAs <c>Phase4ScenarioTests</c> pins),
/// for tests that need no repository.
/// </summary>
internal static class InteractiveList
{
    public const string BaseSha = "4b4622c8ca70c5cfe305043f40ea99c288fbefb3";
    public const string One = "fc6c8fd1a2ee786e8602d9a0ca51ec84f48bfa7b";
    public const string Two = "938844a7a995d3769fbac6d4e9ff4a3095f15898";
    public const string FixOne = "ed58902397c0b9512ef667d9c4299a2cc8757906";
    public const string Three = "ff42e7fddcaf41bcac67a9bd8990e3cb6f9d4ae3";
    public const string Four = "eef5c1549f0e0f729bf7e63734e97eeb0988ba3c";
    public const string Five = "57f089d442eea23fee65c387f560002ab1ccd1fc";

    /// <summary>The list read from Base: One to Five, oldest first, each by Test Author with the message git keeps.</summary>
    public static RebaseList FromBase() => new(
        BaseSha,
        Five,
        [
            Entry(One, "One", "One\n"),
            Entry(Two, "Two", "Two\n"),
            Entry(FixOne, "Fix one", "Fix one\n"),
            Entry(Three, "Three", "Three\n\n#42 is the issue\n"),
            Entry(Four, "Four", "Four\n"),
            Entry(Five, "Five", "Five\n"),
        ],
        MergeCount: 0);

    /// <summary>"Interactive rebase of main from here…" on Base.</summary>
    public static InteractiveRebaseRequest FromHere() =>
        new(new RebaseTarget(BaseSha, "4b4622c"), "refs/heads/main", FromHere: true, CheckoutFirst: false);

    /// <summary>A list of <paramref name="count"/> made-up commits on main, oldest first, named "Commit 1" to "Commit N".</summary>
    public static RebaseList Made(int count, int mergeCount = 0) => new(
        BaseSha,
        ShaOf(count),
        [.. Enumerable.Range(1, count).Select(number => Entry(ShaOf(number), $"Commit {number}", $"Commit {number}\n"))],
        mergeCount);

    /// <summary>A made-up full SHA for commit <paramref name="number"/>.</summary>
    public static string ShaOf(int number) => number.ToString("x8", System.Globalization.CultureInfo.InvariantCulture).PadLeft(40, 'a');

    /// <summary>The own messages of every listed commit, as the git layer reads them for <see cref="RebaseTodo.Build"/>.</summary>
    public static IReadOnlyDictionary<string, string> OwnMessages(RebaseList list) =>
        list.CommitsOldestFirst.ToDictionary(entry => entry.Sha, entry => entry.Message, StringComparer.Ordinal);

    private static RebaseListEntry Entry(string sha, string subject, string message) => new(sha, subject, "Test Author", message);
}

/// <summary>
/// The interactive list's view in the real main window at 1100×700 (the dark theme): a
/// <see cref="ShellDriver"/> on an empty tab, with <see cref="InteractiveRebaseView"/> laid over
/// the graph area (window x 260 to 700, y 88 to 674), as the repository's hook will show it. The
/// window's menu layer, dialog layer and Esc chain serve it as they will in the app. The view
/// model talks to a <see cref="RecordingRebaseHost"/> that shows the app's real dialogs.
/// </summary>
internal sealed class RebaseListHarness : IDisposable
{
    private RebaseListHarness(ShellDriver app, InteractiveRebaseView view, InteractiveRebaseViewModel viewModel, RecordingRebaseHost host)
    {
        App = app;
        View = view;
        ViewModel = viewModel;
        Host = host;
    }

    public ShellDriver App { get; }

    public InteractiveRebaseView View { get; }

    public InteractiveRebaseViewModel ViewModel { get; }

    public RecordingRebaseHost Host { get; }

    /// <summary>
    /// Starts the app on <paramref name="dataDirectory"/> (an empty tab, unless
    /// <paramref name="repository"/> names one to open) and shows the list for
    /// <paramref name="list"/> (the scenario's list from Base when null) over its graph area.
    /// </summary>
    public static async Task<RebaseListHarness> StartAsync(
        string dataDirectory,
        RebaseList? list = null,
        InteractiveRebaseRequest? request = null,
        string? repository = null,
        int width = 1100,
        int height = 700)
    {
        var app = repository is null
            ? ShellDriver.Start(dataDirectory, width, height)
            : ShellDriver.StartWithTabs(dataDirectory, [repository], width: width, height: height);
        await app.WaitUntilReadyAsync();
        if (repository is not null)
        {
            await app.WaitForRepositoryAsync();
        }

        var host = new RecordingRebaseHost(app.Session.Services.DialogHost);
        var viewModel = new InteractiveRebaseViewModel(request ?? InteractiveList.FromHere(), list ?? InteractiveList.FromBase(), "Base", host);
        var view = new InteractiveRebaseView { DataContext = viewModel };
        var graphArea = (CommitGraphView)app.FindByAutomationId("CommitGraph");
        var panel = (Panel)graphArea.Content!;
        panel.Children.Add(view);
        host.OnClose = () => view.IsVisible = false;
        app.Settle();
        await app.WaitForAsync(() => view.Rows.Count == 0 || view.Rows[0].Bounds is not null, "the list's rows to be laid out");
        return new RebaseListHarness(app, view, viewModel, host);
    }

    /// <summary>The list as drawn, in the window's coordinates.</summary>
    public InteractiveRebaseSnapshot Snapshot()
    {
        App.Settle();
        return View.Snapshot(App.Window);
    }

    public void Dispose() => App.Dispose();
}
