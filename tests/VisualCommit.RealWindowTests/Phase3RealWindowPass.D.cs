// Part D's real-window checks (docs/test-reports/phase-3.md, part D, "The real-window pass for
// these checks"): checks 17 and 21 on ONE server and ONE launch (17 with RequireAuth = false, then
// the server switched to RequireAuth = true; check 21's request lists asserted on
// server.Requests.Skip(n), n read just before its step 1); check 21b in a launch of its own (D115);
// checks 28 and 29 step 1 on one launch on sync; check 29 step 3 on ops with `mirror`; checks 19
// and 22 each on sync at the server; check 31 in two launches (RealApp.ClickCloseButton,
// RealApp.WaitForExit). A held fetch's percentage N may differ from the scripted picture's (the
// report's "Percentages"): run.txt reports it. Server scenarios: AuthAtServerAsync and
// SyncAtServerAsync in Phase3RealWindowPass.cs. Only part D's writer edits this file. Shared
// helpers are in Phase3RealWindowPass.cs and RealApp.Checks.cs; part D's own are in the nested
// class PartDSteps, so that they never clash with another part's.
using System.Globalization;
using FlaUI.Core.AutomationElements;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase3;
using Xunit;
using PartD = VisualCommit.VisualTests.Phase3.Phase3Expectations.PartD;

namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
    /// <summary>
    /// Checks 17 (steps 2 and 3) and 21 (steps 1 to 3) on one server and one launch on auth: the
    /// held fetch's progress and Cancel, then, with the server asking for a sign-in, the
    /// credentials dialog, the fetch it lets through, and a fetch cancelled in the dialog.
    /// </summary>
    [Fact]
    public async Task Checks_17_and_21_on_one_server()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        using var app = Launch(scenario.Repo);
        var server = scenario.Server;

        // Check 17 step 2: the server holds the pack of "Fetch origin" from the remote's menu.
        _ = server.HoldNextFetchPack();
        var fetchOrigin = ExpectOperation(app, "auth", "Fetch origin");
        app.RightClickWithMouse(app.RefRow("Remotes", "origin"));
        Assert.Equal(PartDSteps.Labels(PartD.OriginMenu), app.MenuItemNames());
        app.ClickMenuItem("Fetch origin");
        var percent = app.WaitForProgressPercent(OperationTimeout);
        Assert.InRange(percent, 1, 100);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{PartD.FetchingFromOrigin} Counting objects {percent}%"), app.OperationStatusText());
        Run.Note(string.Create(CultureInfo.InvariantCulture, $"17b-fetch-progress: the fetch was held at {percent}% (the scripted picture's N may differ: the report's \"Percentages\")."));
        Compare(app, "17b-fetch-progress");

        // Check 17 step 3: Cancel.
        app.ClickWithMouse(app.Find("OperationCancelButton"));
        fetchOrigin.Wait(OperationOutcome.Cancelled);
        server.Release();
        PartDSteps.WaitForStatus(app, PartD.FetchOriginCancelled);
        Assert.Empty(app.FindAll("ErrorDialog"));
        Compare(app, "17c-fetch-cancelled");

        // Check 21: the server asks for a sign-in from now on; its request list goes on from here.
        server.RequireAuth = true;
        var before = server.Requests.Count;

        // Step 1: the toolbar's Fetch asks.
        var fetch = ExpectOperation(app, "auth", "Fetch");
        app.ClickWithMouse(app.Find("FetchButton"));
        PartDSteps.WaitForElement(app, "CredentialsDialog");
        app.WaitFor(() => app.Find("CredentialUsername").Properties.HasKeyboardFocus.ValueOrDefault, "the user name box to have the focus", RealApp.DefaultWaitTimeout);
        Compare(app, "21a-credentials");
        var requests = server.Requests.Skip(before).ToList();
        var unauthorized = Assert.Single(requests);
        Assert.Equal(("GET", "/remote.git/info/refs", (string?)null, 401), (unauthorized.Method, unauthorized.Path, unauthorized.UserName, unauthorized.Status));

        // Step 2: sign in with the real keyboard.
        app.ClickWithMouse(app.Find("CredentialUsername"));
        app.TypeText("alice");
        app.ClickWithMouse(app.Find("CredentialPassword"));
        app.TypeText("s3cret");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        fetch.Wait(OperationOutcome.Succeeded);
        PartDSteps.WaitForStatus(app, PartD.FetchedFromAll);
        Assert.Empty(app.FindAll("CredentialsDialog"));
        Compare(app, "21b-signed-in");
        requests = [.. server.Requests.Skip(before)];
        Assert.Equal(
            [
                ("GET", "/remote.git/info/refs", (string?)null, (string?)null, 401),
                ("GET", "/remote.git/info/refs", null, "alice", 200),
                ("POST", "/remote.git/git-upload-pack", "ls-refs", "alice", 200),
                ("POST", "/remote.git/git-upload-pack", "fetch", "alice", 200),
            ],
            requests.Select(request => (request.Method, request.Path, request.Command, request.UserName, request.Status)));

        // Step 3: the dialog again; Cancel.
        fetch = ExpectOperation(app, "auth", "Fetch");
        app.ClickWithMouse(app.Find("FetchButton"));
        PartDSteps.WaitForElement(app, "CredentialsDialog");
        app.ClickWithMouse(app.Find("CancelButton"));
        fetch.Wait(OperationOutcome.Cancelled);
        PartDSteps.WaitForStatus(app, PartD.FetchCancelled);
        Assert.Empty(app.FindAll("CredentialsDialog"));
        Assert.Empty(app.FindAll("ErrorDialog"));
        Compare(app, "21c-cancelled");

        Assert.Equal(0, app.Close());
        Assert.DoesNotContain("s3cret", RealApp.ReadLog(app.DataDirectory), StringComparison.Ordinal);
        Assert.Equal(PartD.AuthBehindOne, (await scenario.Repo.GitAsync("status", "-sb")).StandardOutput.Split('\n')[0].TrimEnd('\r'));
    }

    /// <summary>
    /// Check 21b, steps 1 to 3, in a launch of its own (D115): a real fetch through FakeSsh asks
    /// the passphrase, the unknown host and a PIN in the real app's dialogs, through its own helper,
    /// pipe and broker; then the fetch fails as ssh refused the key.
    /// </summary>
    [Fact]
    public async Task Check_21b_prompt_dialogs_through_the_real_helper()
    {
        await using var scenario = await AuthAtServerAsync();
        var fake = await FakeSsh.AddRemoteAsync(scenario.Repo);
        using var app = Launch(scenario.Repo);

        // Step 1: "Fetch fake-ssh"; the passphrase.
        var fetch = ExpectOperation(app, "auth", "Fetch fake-ssh");
        app.RightClickWithMouse(app.RefRow("Remotes", FakeSsh.RemoteName));
        app.ClickMenuItem("Fetch fake-ssh");
        PartDSteps.WaitForElement(app, "PassphraseDialog");
        app.WaitFor(() => app.Find("PassphraseBox").Properties.HasKeyboardFocus.ValueOrDefault, "the passphrase box to have the focus", RealApp.DefaultWaitTimeout);
        Compare(app, "21b1-passphrase");
        app.TypeText("s3cret");
        app.ClickWithMouse(app.Find("ConfirmButton"));

        // Step 2: the unknown host.
        PartDSteps.WaitForElement(app, "HostKeyDialog");
        Compare(app, "21b2-unknown-host");
        app.ClickWithMouse(app.Find("ConfirmButton"));

        // Step 3: the PIN, then the failed fetch.
        PartDSteps.WaitForElement(app, "PromptDialog");
        app.WaitFor(() => app.Find("PromptBox").Properties.HasKeyboardFocus.ValueOrDefault, "the prompt's box to have the focus", RealApp.DefaultWaitTimeout);
        Compare(app, "21b3-prompt");
        app.TypeText("123456");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        fetch.Wait(OperationOutcome.Failed);
        PartDSteps.WaitForElement(app, "ErrorDialog");
        Compare(app, "21b4-fetch-failed");
        Assert.Equal(["s3cret", "yes", "123456"], fake.Answers);
        app.ClickWithMouse(app.Find("ConfirmButton"));

        Assert.Equal(0, app.Close());
        var log = RealApp.ReadLog(app.DataDirectory);
        Assert.DoesNotContain("s3cret", log, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", log, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks 28 (steps 1 and 2) and 29 (step 1) on one launch on sync: topic's menu and the pick
    /// dialog with the filter typed, cancelled so that topic keeps no upstream; then the push dialog
    /// of topic, cancelled.
    /// </summary>
    [Fact]
    public async Task Checks_28_and_29_step_1_on_one_launch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = Launch(repo);

        // Check 28 step 1: topic's menu.
        app.RightClickWithMouse(app.RefRow("Local branches", "topic"));
        Assert.Equal(PartDSteps.Labels(PartD.TopicMenu), app.MenuItemNames());
        Compare(app, "28a-topic-menu", keepPointer: true);

        // Step 2: the pick dialog, "pub" typed, the item clicked; then Cancel.
        app.ClickMenuItem("Set upstream…");
        PartDSteps.WaitForElement(app, "PickDialog");
        app.WaitFor(() => app.Find("PickFilter").Properties.HasKeyboardFocus.ValueOrDefault, "the filter to have the focus", RealApp.DefaultWaitTimeout);
        app.TypeText("pub");
        app.WaitFor(() => app.FindAll("PickItem").Count == 1, "the filtered list", RealApp.DefaultWaitTimeout);
        app.ClickWithMouse(app.FindAll("PickItem").Single(item => item.Name == "origin/published"));
        Compare(app, "28b-pick");
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("PickDialog").Count == 0, "the pick dialog to close", RealApp.DefaultWaitTimeout);
        Assert.False((await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.topic\.")).Succeeded, "Cancel set an upstream.");

        // Check 29 step 1: the push dialog of topic; then Cancel.
        app.RightClickWithMouse(app.RefRow("Local branches", "topic"));
        app.ClickMenuItem("Push topic…");
        PartDSteps.WaitForElement(app, "PushDialog");
        app.WaitFor(() => app.Find("PushBranchBox").Properties.HasKeyboardFocus.ValueOrDefault, "the branch box to have the focus", RealApp.DefaultWaitTimeout);
        Compare(app, "29a-push-dialog");
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("PushDialog").Count == 0, "the push dialog to close", RealApp.DefaultWaitTimeout);

        Assert.Equal(0, app.Close());
        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/topic"));
    }

    /// <summary>Check 29, step 3, on ops with a second remote <c>mirror</c>: the choice dialog of "Push v1.0…", cancelled.</summary>
    [Fact]
    public async Task Check_29_step_3_the_choice_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var app = Launch(repo);

        app.RightClickWithMouse(app.RefRow("Tags", "v1.0"));
        Assert.Equal(PartDSteps.Labels(PartD.TagMenu), app.MenuItemNames());
        app.ClickMenuItem("Push v1.0…");
        PartDSteps.WaitForElement(app, "ChoiceDialog");
        Assert.Equal(["mirror", "origin"], app.FindAll("ChoiceOption").Select(option => option.Name));
        Compare(app, "29c-choice");
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("ChoiceDialog").Count == 0, "the choice dialog to close", RealApp.DefaultWaitTimeout);

        Assert.Equal(0, app.Close());
        Assert.Equal(string.Empty, (await repo.GitAsync("ls-remote", "--tags", "origin")).StandardOutput.Trim());
    }

    /// <summary>Check 19, steps 1 and 2, on sync at the server: the push rejected, then "Pull" in the dialog.</summary>
    [Fact]
    public async Task Check_19_push_rejected_then_pull()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = Launch(repo);

        // Step 1: the push is rejected. "Copy output" is not clicked: it would change the owner's clipboard.
        var push = ExpectOperation(app, "sync", "Push main");
        app.ClickWithMouse(app.Find("PushButton"));
        push.Wait(OperationOutcome.Failed);
        PartDSteps.WaitForElement(app, "ErrorDialog");
        Assert.Equal("Push rejected", app.Find("ErrorTitle").Name);
        Assert.Equal("Pull", app.Find("ErrorExtraButton").Name);
        Compare(app, "19a-push-rejected");

        // Step 2: "Pull".
        var pull = ExpectOperation(app, "sync", "Pull");
        app.ClickWithMouse(app.Find("ErrorExtraButton"));
        pull.Wait(OperationOutcome.Succeeded);
        PartDSteps.WaitForStatus(app, PartD.PulledAndMerged);
        Assert.Empty(app.FindAll("ErrorDialog"));
        Compare(app, "19b-pulled");

        Assert.Equal(0, app.Close());
        Assert.Equal(Phase3Expectations.WithPort(PartD.MergeSubject, scenario.Server.Port), (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    /// <summary>
    /// Check 22, steps 1 to 5, on sync at the server: the Remotes dialog, the add form typed with
    /// the real keyboard, the remote added and fetched, its URL replaced with one holding a token
    /// (Ctrl+A and typing), renamed, and removed through the confirmation stacked over the dialog.
    /// </summary>
    [Fact]
    public async Task Check_22_remotes_dialog()
    {
        await using var scenario = await SyncAtServerAsync();
        var repo = scenario.Repo;
        var serverUrl = Phase3Expectations.WithPort(PartD.ServerUrl, scenario.Server.Port);
        using var app = Launch(repo);

        // Step 1: the section header's menu, "Manage remotes…".
        app.RightClickWithMouse(app.RefRow("Remotes"));
        Assert.Equal(PartDSteps.Labels(PartD.RemotesHeaderMenu), app.MenuItemNames());
        app.ClickMenuItem("Manage remotes…");
        PartDSteps.WaitForRemoteRows(app, "origin");
        Compare(app, "22a-remotes");

        // Step 2: the add form, typed; Save.
        app.ClickWithMouse(app.Find("AddRemoteButton"));
        app.WaitFor(() => app.Find("RemoteNameBox").Properties.HasKeyboardFocus.ValueOrDefault, "the name box to have the focus", RealApp.DefaultWaitTimeout);
        app.TypeText("mirror");
        app.ClickWithMouse(app.Find("RemoteUrlBox"));
        app.TypeText(serverUrl);
        Compare(app, "22b-add-form");
        var fetchMirror = ExpectOperation(app, "sync", "Fetch mirror");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        fetchMirror.Wait(OperationOutcome.Succeeded);
        PartDSteps.WaitForStatus(app, PartD.FetchedFromMirror);
        PartDSteps.WaitForRemoteRows(app, "mirror", "origin");
        Compare(app, "22c-mirror-added");

        // Step 3: a URL with a token, typed after Ctrl+A; shown hidden.
        app.ClickWithMouse(PartDSteps.RemoteRowButton(app, "mirror", "RemoteEditButton"));
        app.ReplaceText("RemoteUrlBox", PartD.TokenUrl);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        PartDSteps.WaitForStatus(app, PartD.ChangedUrlOfMirror);
        PartDSteps.WaitForRemoteRows(app, "mirror", "origin");
        app.WaitFor(() => PartDSteps.RemoteUrlOf(app, "mirror") == PartD.HiddenTokenUrl, "mirror's URL to show hidden", RealApp.DefaultWaitTimeout);
        Compare(app, "22d-token-hidden");

        // Step 4: renamed.
        app.ClickWithMouse(PartDSteps.RemoteRowButton(app, "mirror", "RemoteEditButton"));
        app.ReplaceText("RemoteNameBox", "backup");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        PartDSteps.WaitForStatus(app, PartD.RenamedMirror);
        PartDSteps.WaitForRemoteRows(app, "backup", "origin");

        // Step 5: Remove asks on top of the dialog; confirmed.
        app.ClickWithMouse(PartDSteps.RemoteRowButton(app, "backup", "RemoteRemoveButton"));
        PartDSteps.WaitForElement(app, "ConfirmationDialog");
        Assert.Single(app.FindAll("RemotesDialog"));
        Compare(app, "22e-remove-stacked");
        var confirmation = app.Find("ConfirmationDialog");
        app.ClickWithMouse(confirmation.FindFirstDescendant(condition => condition.ByAutomationId("ConfirmButton"))
            ?? throw new InvalidOperationException("The confirmation has no ConfirmButton."));
        PartDSteps.WaitForStatus(app, PartD.RemovedBackup);
        app.WaitFor(() => app.FindAll("ConfirmationDialog").Count == 0, "the confirmation to close", RealApp.DefaultWaitTimeout);
        PartDSteps.WaitForRemoteRows(app, "origin");
        Compare(app, "22f-removed");

        Assert.Equal(0, app.Close());
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        Assert.DoesNotContain(PartD.Token, RealApp.ReadLog(app.DataDirectory), StringComparison.Ordinal);
    }

    /// <summary>
    /// Check 31, steps 1 to 4: the window's close button clicked while a held fetch runs and the
    /// toolbar's Fetch waits; the finishing dialog, "Keep open", then the close again and the app
    /// exiting by itself once the fetch is let go.
    /// </summary>
    [Fact]
    public async Task Check_31_finishing_dialog_and_closing_by_itself()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        using var app = Launch(scenario.Repo);

        // Step 1.
        var percent = PartDSteps.HoldFetchAndQueueAnother(app, scenario.Server);
        Run.Note(string.Create(CultureInfo.InvariantCulture, $"31a-finishing, 31b-kept-open: the fetch was held at {percent}% (the scripted pictures' N may differ)."));

        // Step 2: the close button; the finishing dialog.
        app.ClickCloseButton();
        PartDSteps.WaitForElement(app, "FinishingDialog");
        Assert.Equal([PartD.FinishingItem], app.FindAll("FinishingItem").Select(item => item.Name));
        app.WaitFor(() => app.FindAll("OperationWaiting").Count == 0, "\"1 waiting\" to go", RealApp.DefaultWaitTimeout);
        Compare(app, "31a-finishing");

        // Step 3: "Keep open".
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("FinishingDialog").Count == 0, "the finishing dialog to close", RealApp.DefaultWaitTimeout);
        Assert.False(app.HasExited, "The app exited after \"Keep open\".");
        Compare(app, "31b-kept-open");

        // Step 4: close again; let the fetch go; the app exits by itself.
        app.ClickCloseButton();
        PartDSteps.WaitForElement(app, "FinishingDialog");
        scenario.Server.Release();
        Assert.Equal(0, app.WaitForExit(RealApp.DefaultWaitTimeout));
        Assert.Contains(
            AppLogFile.ReadLines(app.DataDirectory),
            line => AppLogFile.OperationEnded("auth", "Fetch origin", OperationOutcome.Succeeded, PartD.FetchedFromOrigin).IsMatch(line));
        Assert.Single(scenario.Server.Requests, request => request.Command == "fetch");
        Assert.Equal(PartD.AuthBehindOne, (await scenario.Repo.GitAsync("status", "-sb")).StandardOutput.Split('\n')[0].TrimEnd('\r'));
    }

    /// <summary>Check 31, step 5, in a second launch on a new copy: steps 1 and 2 again, then "Quit now"; the app exits within 10 seconds (D36) and leaves no git running.</summary>
    [Fact]
    public async Task Check_31_quit_now()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        using var app = Launch(scenario.Repo);

        PartDSteps.HoldFetchAndQueueAnother(app, scenario.Server);
        var children = app.DescendantProcesses();
        Run.Note($"31 quit now: the app's processes while the fetch was held: {string.Join(", ", children.Select(child => child.Name))}");
        app.ClickCloseButton();
        PartDSteps.WaitForElement(app, "FinishingDialog");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        Assert.Equal(0, app.WaitForExit(TimeSpan.FromSeconds(10)));

        // No git process the app started is left (they end with it, or a moment after).
        var deadline = DateTime.UtcNow + RealApp.DefaultWaitTimeout;
        while (RealApp.StillRunning(children.Select(child => child.Id)).Count > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Assert.Empty(RealApp.StillRunning(children.Select(child => child.Id)));
        scenario.Server.Release();
        Assert.Equal(
            (await scenario.Repo.GitAsync("rev-parse", "refs/heads/main")).StandardOutput.Trim(),
            (await scenario.Repo.GitAsync("rev-parse", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(scenario.Repo.Path, ".git"), "*.lock", SearchOption.AllDirectories));
    }

    /// <summary>What part D's real-window checks share, apart from the other parts' helpers.</summary>
    private static class PartDSteps
    {
        /// <summary>The labels of a menu written as the report writes it: its items, without the header, separators and the "(danger)" and "(disabled: …)" notes.</summary>
        public static IReadOnlyList<string> Labels(IReadOnlyList<string> lines) =>
            [.. lines.Skip(1).Where(line => line != "—").Select(line =>
            {
                var note = line.IndexOf(" (d", StringComparison.Ordinal);
                return note >= 0 && (line[note..].StartsWith(" (danger)", StringComparison.Ordinal) || line[note..].StartsWith(" (disabled", StringComparison.Ordinal))
                    ? line[..note]
                    : line;
            })];

        /// <summary>Waits until an element with this automation id is in the window, at most an operation's time.</summary>
        public static AutomationElement WaitForElement(RealApp app, string automationId)
        {
            app.WaitFor(() => app.FindAll(automationId).Count > 0, automationId, OperationTimeout);
            return app.Find(automationId);
        }

        /// <summary>Waits until the status bar's operation text is <paramref name="text"/> (it follows the queue's log line on the UI thread).</summary>
        public static void WaitForStatus(RealApp app, string text) =>
            app.WaitFor(() => app.OperationStatusText() == text, $"the status bar to say \"{text}\" (it says \"{app.OperationStatusText()}\")", OperationTimeout);

        /// <summary>The Remotes dialog's rows (<c>RemoteRow</c>, named by the remote), top to bottom.</summary>
        public static IReadOnlyList<string> RemoteRowNames(RealApp app) =>
            [.. app.FindAll("RemoteRow").OrderBy(row => row.BoundingRectangle.Top).Select(row => row.Name)];

        /// <summary>Waits until the Remotes dialog lists exactly these remotes.</summary>
        public static void WaitForRemoteRows(RealApp app, params string[] names) =>
            app.WaitFor(() => RemoteRowNames(app).SequenceEqual(names), "the Remotes dialog to list " + string.Join(", ", names), OperationTimeout);

        /// <summary>A button of a remote's row (<c>RemoteEditButton</c>, <c>RemoteRemoveButton</c>).</summary>
        public static AutomationElement RemoteRowButton(RealApp app, string remote, string automationId) =>
            app.FindAll("RemoteRow").Single(row => row.Name == remote).FindFirstDescendant(condition => condition.ByAutomationId(automationId))
                ?? throw new InvalidOperationException($"The row of {remote} has no {automationId}.");

        /// <summary>The URL a remote's row shows.</summary>
        public static string? RemoteUrlOf(RealApp app, string remote) =>
            app.FindAll("RemoteRow").SingleOrDefault(row => row.Name == remote)?.FindFirstDescendant(condition => condition.ByAutomationId("RemoteUrl"))?.Name;

        /// <summary>
        /// Check 31 step 1: the server holds the next pack, "Fetch origin" from the remote's menu
        /// runs to a percentage, and the toolbar's Fetch waits behind it ("1 waiting"). Returns N.
        /// </summary>
        public static int HoldFetchAndQueueAnother(RealApp app, AuthRemoteServer server)
        {
            _ = server.HoldNextFetchPack();
            app.RightClickWithMouse(app.RefRow("Remotes", "origin"));
            app.ClickMenuItem("Fetch origin");
            var percent = app.WaitForProgressPercent(OperationTimeout);
            app.ClickWithMouse(app.Find("FetchButton"));
            app.WaitFor(() => app.FindAll("OperationWaiting").Any(element => element.Name == PartD.OneWaiting), "\"1 waiting\"", RealApp.DefaultWaitTimeout);
            return percent;
        }
    }
}
