using System.Collections.Concurrent;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="PromptDialogs"/> without its dialogs (docs/phase-3-design.md 3.2): what one
/// operation's dialogs were told answers the prompts that follow, for that scope and for two
/// minutes only; the app session's broker is the askpass broker; the welcome page's clone opens
/// a prompt scope. With real git against <see cref="AuthRemoteServer"/> where git takes part.
/// </summary>
public class PromptDialogsTests
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public void The_helper_is_in_the_output_folder_and_the_session_broker_finds_it()
    {
        // Risk 8 of 7.7: the helper reaches every test project's output through the reference.
        Assert.NotNull(AskPassLocator.Find());
        Assert.True(File.Exists(Path.Combine(AskPassLocator.Folder, "visualcommit-askpass.runtimeconfig.json")));

        var broker = PromptDialogs.CreateBroker(new PromptDialogs(new DialogHostViewModel()), NullAppLog.Instance);
        try
        {
            var askPass = Assert.IsType<AskPassBroker>(broker);
            Assert.True(askPass.HelperFound);
            Assert.Equal(AskPassLocator.Find(), askPass.HelperPath);
        }
        finally
        {
            (broker as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public async Task The_password_typed_with_the_user_name_answers_gits_password_prompt_of_the_same_scope()
    {
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("alice", "s3cret"), new PromptDialogAnswer("s3cret-b"));
        var dialogs = new PromptDialogs(presenter);

        var user = await dialogs.AnswerAsync(Prompt(GitPromptKind.Username, "Username for 'https://example.com': ", "op-1", url: "https://example.com"), TestCancelled);
        var password = await dialogs.AnswerAsync(Prompt(GitPromptKind.Password, "Password for 'https://alice@example.com': ", "op-1"), TestCancelled);
        var otherScope = await dialogs.AnswerAsync(Prompt(GitPromptKind.Password, "Password for 'https://alice@example.com': ", "op-2"), TestCancelled);

        Assert.Equal(PromptAnswer.Of("alice"), user);
        Assert.Equal(PromptAnswer.Of("s3cret"), password);
        Assert.Equal(PromptAnswer.Of("s3cret-b"), otherScope);
        Assert.Equal(["op-1", "op-2"], presenter.Shown.Select(prompt => prompt.ScopeId));
    }

    [Fact]
    public async Task An_answer_answers_the_same_text_again_for_two_minutes_and_only_in_its_scope()
    {
        var clock = new ManualClock();
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("first"), new PromptDialogAnswer("second"), new PromptDialogAnswer("third"));
        var dialogs = new PromptDialogs(presenter, clock: clock);
        var prompt = Prompt(GitPromptKind.Passphrase, "Enter passphrase for key '/home/u/.ssh/id_ed25519': ", "op-1");

        Assert.Equal(PromptAnswer.Of("first"), await dialogs.AnswerAsync(prompt, TestCancelled));
        clock.Advance(PromptDialogs.KeepAnswersFor - TimeSpan.FromSeconds(1));
        Assert.Equal(PromptAnswer.Of("first"), await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(PromptAnswer.Of("second"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-2" }, TestCancelled));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PromptAnswer.Of("third"), await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(3, presenter.Shown.Count);
    }

    [Fact]
    public async Task What_a_scope_kept_is_forgotten_when_the_scope_ends()
    {
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("alice", "s3cret"), new PromptDialogAnswer("again"));
        var dialogs = new PromptDialogs(presenter);
        using var scope = new CancellationTokenSource();
        var prompt = Prompt(GitPromptKind.Username, "Username for 'https://example.com': ", "op-1", url: "https://example.com");

        await dialogs.AnswerAsync(prompt, scope.Token);
        await scope.CancelAsync();

        Assert.Equal(PromptAnswer.Of("again"), await dialogs.AnswerAsync(Prompt(GitPromptKind.Password, "Password for 'https://alice@example.com': ", "op-1"), TestCancelled));
        Assert.Equal(2, presenter.Shown.Count);
    }

    [Fact]
    public async Task Cancel_is_reported_as_the_users_and_nothing_is_kept()
    {
        var presenter = new ScriptedPresenter(PromptDialogAnswer.CancelledByUser, PromptDialogAnswer.None, new PromptDialogAnswer("x"));
        var dialogs = new PromptDialogs(presenter);
        var prompt = Prompt(GitPromptKind.Other, "Verification code: ", "op-1");

        Assert.Equal(PromptAnswer.Cancelled, await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(PromptAnswer.Refused, await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(PromptAnswer.Of("x"), await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(3, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_notice_is_shown_until_its_scope_ends_and_gets_no_answer()
    {
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("ignored"));
        var dialogs = new PromptDialogs(presenter);

        var answer = await dialogs.AnswerAsync(Prompt(GitPromptKind.Notice, "Confirm user presence for key ED25519-SK SHA256:x", "op-1"), TestCancelled);

        Assert.Equal(PromptAnswer.Refused, answer);
        Assert.Equal(GitPromptKind.Notice, Assert.Single(presenter.Shown).Kind);
    }

    // Prompt groups (docs/phase-6-design.md 4.4, D210): a bulk run's prompts, one at a time, and
    // what the run was told answers the same text in its other repositories.

    [Fact]
    public async Task Two_prompts_of_one_group_with_the_same_text_show_one_dialog_and_the_password_follows()
    {
        using var group = new PromptGroup();
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("alice", "s3cret"), new PromptDialogAnswer("never"));
        var dialogs = new PromptDialogs(presenter);
        var user = Prompt(GitPromptKind.Username, "Username for 'https://example.com': ", "op-1", url: "https://example.com") with { Group = group };
        var password = Prompt(GitPromptKind.Password, "Password for 'https://alice@example.com': ", "op-1") with { Group = group };

        Assert.Equal(PromptAnswer.Of("alice"), await dialogs.AnswerAsync(user, TestCancelled));
        Assert.Equal(PromptAnswer.Of("s3cret"), await dialogs.AnswerAsync(password, TestCancelled));

        // Another repository of the run, another scope: the same texts, the same credential.
        Assert.Equal(PromptAnswer.Of("alice"), await dialogs.AnswerAsync(user with { ScopeId = "op-2", RepositoryName = "web" }, TestCancelled));
        Assert.Equal(PromptAnswer.Of("s3cret"), await dialogs.AnswerAsync(password with { ScopeId = "op-2", RepositoryName = "web" }, TestCancelled));
        Assert.Equal("op-1", Assert.Single(presenter.Shown).ScopeId);
    }

    [Fact]
    public async Task A_groups_second_prompt_waits_for_the_first_ones_answer_and_a_different_text_asks_again()
    {
        using var group = new PromptGroup();
        var presenter = new HeldPresenter();
        var dialogs = new PromptDialogs(presenter);
        var first = dialogs.AnswerAsync(Prompt(GitPromptKind.Username, "Username for 'https://example.com': ", "op-1", url: "https://example.com") with { Group = group }, TestCancelled);
        var shownFirst = await presenter.NextAsync();

        var second = dialogs.AnswerAsync(Prompt(GitPromptKind.Username, "Username for 'https://other.example': ", "op-2", url: "https://other.example") with { Group = group }, TestCancelled);
        var alone = dialogs.AnswerAsync(Prompt(GitPromptKind.Username, "Username for 'https://other.example': ", "op-3", url: "https://other.example"), TestCancelled);
        var shownAlone = await presenter.NextAsync();

        // A prompt without a group is not held back by one; the group's second one is.
        Assert.Equal("op-3", shownAlone.Prompt.ScopeId);
        Assert.False(second.IsCompleted);
        Assert.Equal(2, presenter.Shown.Count);

        shownFirst.Answer(new PromptDialogAnswer("alice", "s3cret"));
        Assert.Equal(PromptAnswer.Of("alice"), await first.WaitAsync(StepTimeout, TestCancelled));
        var shownSecond = await presenter.NextAsync();
        Assert.Equal("op-2", shownSecond.Prompt.ScopeId);
        shownSecond.Answer(new PromptDialogAnswer("bob", "pw"));
        shownAlone.Answer(new PromptDialogAnswer("carol", "pw2"));

        Assert.Equal(PromptAnswer.Of("bob"), await second.WaitAsync(StepTimeout, TestCancelled));
        Assert.Equal(PromptAnswer.Of("carol"), await alone.WaitAsync(StepTimeout, TestCancelled));
        Assert.Equal(3, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_Cancel_answers_the_same_text_of_the_group_with_Cancelled_and_another_text_still_asks()
    {
        using var group = new PromptGroup();
        var presenter = new ScriptedPresenter(PromptDialogAnswer.CancelledByUser, new PromptDialogAnswer("123456"));
        var dialogs = new PromptDialogs(presenter);
        var user = Prompt(GitPromptKind.Username, "Username for 'https://example.com': ", "op-1", url: "https://example.com") with { Group = group };

        Assert.Equal(PromptAnswer.Cancelled, await dialogs.AnswerAsync(user, TestCancelled));
        Assert.Equal(PromptAnswer.Cancelled, await dialogs.AnswerAsync(user with { ScopeId = "op-2" }, TestCancelled));
        Assert.Equal(PromptAnswer.Of("123456"), await dialogs.AnswerAsync(Prompt(GitPromptKind.Other, "Verification code: ", "op-3") with { Group = group }, TestCancelled));
        Assert.Equal(2, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_groups_answers_are_forgotten_after_two_minutes()
    {
        using var group = new PromptGroup();
        var clock = new ManualClock();
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("first"), new PromptDialogAnswer("second"));
        var dialogs = new PromptDialogs(presenter, clock: clock);
        var prompt = Prompt(GitPromptKind.Passphrase, "Enter passphrase for key '/home/u/.ssh/id_ed25519': ", "op-1") with { Group = group };

        Assert.Equal(PromptAnswer.Of("first"), await dialogs.AnswerAsync(prompt, TestCancelled));
        clock.Advance(PromptDialogs.KeepAnswersFor - TimeSpan.FromSeconds(1));
        Assert.Equal(PromptAnswer.Of("first"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-2" }, TestCancelled));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(PromptAnswer.Of("second"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-3" }, TestCancelled));
        Assert.Equal(2, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_groups_answers_are_forgotten_when_it_ends_and_its_later_prompts_are_asked_alone()
    {
        var group = new PromptGroup();
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("first"), new PromptDialogAnswer("second"), new PromptDialogAnswer("third"));
        var dialogs = new PromptDialogs(presenter);
        var prompt = Prompt(GitPromptKind.Other, "Verification code: ", "op-1") with { Group = group };
        Assert.Equal(PromptAnswer.Of("first"), await dialogs.AnswerAsync(prompt, TestCancelled));

        group.Dispose();

        // As a prompt without a group: a dialog, and its answer kept for its scope only.
        Assert.Equal(PromptAnswer.Of("second"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-2" }, TestCancelled));
        Assert.Equal(PromptAnswer.Of("second"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-2" }, TestCancelled));
        Assert.Equal(PromptAnswer.Of("third"), await dialogs.AnswerAsync(prompt with { ScopeId = "op-3" }, TestCancelled));
        Assert.Equal(3, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_prompt_waiting_for_its_groups_turn_is_refused_when_its_scope_ends_and_shows_nothing()
    {
        using var group = new PromptGroup();
        var presenter = new HeldPresenter();
        var dialogs = new PromptDialogs(presenter);
        var first = dialogs.AnswerAsync(Prompt(GitPromptKind.Other, "Verification code: ", "op-1") with { Group = group }, TestCancelled);
        var shownFirst = await presenter.NextAsync();
        using var scope = new CancellationTokenSource();
        var waiting = dialogs.AnswerAsync(Prompt(GitPromptKind.Other, "Another code: ", "op-2") with { Group = group }, scope.Token);

        await scope.CancelAsync();

        Assert.Equal(PromptAnswer.Refused, await waiting.WaitAsync(StepTimeout, TestCancelled));
        shownFirst.Answer(new PromptDialogAnswer("1"));
        Assert.Equal(PromptAnswer.Of("1"), await first.WaitAsync(StepTimeout, TestCancelled));
        Assert.Single(presenter.Shown);
    }

    [Fact]
    public async Task A_dialog_closed_because_its_scope_ended_keeps_nothing_for_the_group()
    {
        using var group = new PromptGroup();
        var presenter = new HeldPresenter();
        var dialogs = new PromptDialogs(presenter);
        var prompt = Prompt(GitPromptKind.Other, "Verification code: ", "op-1") with { Group = group };
        using var scope = new CancellationTokenSource();
        var first = dialogs.AnswerAsync(prompt, scope.Token);
        var shownFirst = await presenter.NextAsync();

        // The presenter closes its dialog when the scope ends: no answer, never a Cancel of the user.
        await scope.CancelAsync();
        shownFirst.Answer(PromptDialogAnswer.None);
        Assert.Equal(PromptAnswer.Refused, await first.WaitAsync(StepTimeout, TestCancelled));

        var again = dialogs.AnswerAsync(prompt with { ScopeId = "op-2" }, TestCancelled);
        var shownAgain = await presenter.NextAsync();
        shownAgain.Answer(new PromptDialogAnswer("2"));
        Assert.Equal(PromptAnswer.Of("2"), await again.WaitAsync(StepTimeout, TestCancelled));
    }

    [Fact]
    public async Task A_groups_answer_never_answers_a_prompt_without_the_group_nor_the_other_way_round()
    {
        using var group = new PromptGroup();
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("grouped"), new PromptDialogAnswer("alone"));
        var dialogs = new PromptDialogs(presenter);
        var prompt = Prompt(GitPromptKind.Other, "Verification code: ", "op-1");

        Assert.Equal(PromptAnswer.Of("grouped"), await dialogs.AnswerAsync(prompt with { Group = group }, TestCancelled));
        Assert.Equal(PromptAnswer.Of("alone"), await dialogs.AnswerAsync(prompt, TestCancelled));
        Assert.Equal(PromptAnswer.Of("grouped"), await dialogs.AnswerAsync(prompt with { Group = group, ScopeId = "op-2" }, TestCancelled));
        Assert.Equal(2, presenter.Shown.Count);
    }

    [Fact]
    public async Task A_notice_of_a_group_shows_at_once_beside_a_groups_open_dialog()
    {
        using var group = new PromptGroup();
        var presenter = new HeldPresenter();
        var dialogs = new PromptDialogs(presenter);
        var first = dialogs.AnswerAsync(Prompt(GitPromptKind.Other, "Verification code: ", "op-1") with { Group = group }, TestCancelled);
        var shownFirst = await presenter.NextAsync();

        var notice = dialogs.AnswerAsync(Prompt(GitPromptKind.Notice, "Confirm user presence for key ED25519-SK SHA256:x", "op-2") with { Group = group }, TestCancelled);
        var shownNotice = await presenter.NextAsync();

        Assert.Equal(GitPromptKind.Notice, shownNotice.Prompt.Kind);
        shownNotice.Answer(PromptDialogAnswer.None);
        Assert.Equal(PromptAnswer.Refused, await notice.WaitAsync(StepTimeout, TestCancelled));
        shownFirst.Answer(new PromptDialogAnswer("1"));
        Assert.Equal(PromptAnswer.Of("1"), await first.WaitAsync(StepTimeout, TestCancelled));
    }

    [Fact]
    public async Task A_fetch_signs_in_with_one_dialog_and_a_second_contact_of_the_operation_asks_nothing()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var presenter = new ScriptedPresenter(new PromptDialogAnswer("alice", "s3cret"));
        using var broker = new AskPassBroker(new PromptDialogs(presenter));
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask);

        var fetch = await GitAsync(repo, scope.Environment, "fetch", "origin");
        var listed = await GitAsync(repo, scope.Environment, "ls-remote", "origin", "refs/heads/main");

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.True(listed.Succeeded, listed.StandardError);
        var shown = Assert.Single(presenter.Shown);
        Assert.Equal(GitPromptKind.Username, shown.Kind);
        Assert.Equal($"127.0.0.1:{server.Port}", GitPromptClassifier.HostOf(shown.Url!));
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "alice");
    }

    [Fact]
    public async Task The_welcome_pages_clone_passes_a_prompt_scopes_variables_and_closes_the_scope()
    {
        var broker = new RecordingBroker();
        var provider = new RecordingProvider(new GitException("git clone", 128, "fatal: repository not found"));
        var welcome = CreateWelcome(broker, provider);

        await welcome.StartCloneCommand.ExecuteAsync(null);

        var scope = Assert.Single(broker.Scopes);
        Assert.Equal((PromptMode.Ask, "Clone", "x"), (scope.Mode, scope.Title, scope.RepositoryName));
        Assert.Null(scope.Group);
        Assert.Same(scope.Environment, provider.Environment);
        Assert.True(scope.IsDisposed);
        Assert.Equal("fatal: repository not found", welcome.ErrorText);
    }

    [Fact]
    public async Task A_clone_whose_prompt_the_user_cancelled_says_it_was_cancelled()
    {
        var broker = new RecordingBroker();
        var provider = new RecordingProvider(new GitException("git clone", 128, "fatal: could not read Username for 'https://example.com': terminal prompts disabled"));
        provider.BeforeFailing = () => broker.Scopes[0].CancelByUser();
        var welcome = CreateWelcome(broker, provider);

        await welcome.StartCloneCommand.ExecuteAsync(null);

        Assert.Equal("Clone cancelled.", welcome.ErrorText);
        Assert.True(broker.Scopes[0].IsDisposed);
    }

    private static WelcomeViewModel CreateWelcome(IGitPromptBroker broker, IRepositoryProvider provider)
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        var session = new MemorySession();
        var services = new TabServices(provider, new NoFolders(), settings, session, new DateDisplay(TimeZoneInfo.Utc), prompts: broker);
        var shell = new MainWindowViewModel(
            settings,
            new MainWindowViewModelTests.FakeThemes(),
            _ => Task.FromResult(new GitDetection(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null)),
            NullAppLog.Instance,
            services,
            session);
        var welcome = shell.ActiveTab.Welcome;
        welcome.CloneUrl = "https://example.com/x.git";
        welcome.CloneParent = Path.GetTempPath();
        return welcome;
    }

    private static GitPrompt Prompt(GitPromptKind kind, string text, string scopeId, string? url = null) =>
        new(kind, text, scopeId, "Fetch", "repo", url ?? GitPromptClassifier.Classify(text, null).Url, GitPromptClassifier.Classify(text, null).User);

    private static Task<GitResult> GitAsync(TempRepo repo, IReadOnlyDictionary<string, string?> scopeEnvironment, params string[] arguments)
    {
        var environment = new Dictionary<string, string?>(repo.Environment);
        foreach (var variable in new[] { "GIT_ASKPASS", "SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "GCM_INTERACTIVE", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy" })
        {
            environment[variable] = null;
        }

        environment["no_proxy"] = "127.0.0.1,localhost";
        environment["NO_PROXY"] = "127.0.0.1,localhost";
        foreach (var (name, value) in scopeEnvironment)
        {
            environment[name] = value;
        }

        var command = new GitCommand(arguments) { WorkingDirectory = repo.Path, Environment = environment };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        timeout.CancelAfter(StepTimeout);
        return repo.Runner.RunAsync(command, timeout.Token);
    }

    /// <summary>Answers each dialog from a script and records what it was asked to show.</summary>
    private sealed class ScriptedPresenter(params PromptDialogAnswer[] answers) : IPromptDialogPresenter
    {
        private readonly ConcurrentQueue<PromptDialogAnswer> _answers = new(answers);
        private readonly ConcurrentQueue<GitPrompt> _shown = new();

        public IReadOnlyList<GitPrompt> Shown => [.. _shown];

        public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken)
        {
            _shown.Enqueue(prompt);
            return Task.FromResult(_answers.TryDequeue(out var answer) ? answer : PromptDialogAnswer.None);
        }
    }

    /// <summary>Holds every dialog open until the test answers it; <see cref="NextAsync"/> gives the dialogs in the order they were asked for.</summary>
    private sealed class HeldPresenter : IPromptDialogPresenter
    {
        private readonly ConcurrentQueue<GitPrompt> _shown = new();
        private readonly System.Threading.Channels.Channel<HeldDialog> _dialogs = System.Threading.Channels.Channel.CreateUnbounded<HeldDialog>();

        public IReadOnlyList<GitPrompt> Shown => [.. _shown];

        public Task<PromptDialogAnswer> ShowAsync(GitPrompt prompt, CancellationToken cancellationToken)
        {
            _shown.Enqueue(prompt);
            var dialog = new HeldDialog(prompt);
            _dialogs.Writer.TryWrite(dialog);
            return dialog.Answered.Task;
        }

        /// <summary>The next dialog asked for, waiting for it.</summary>
        public async Task<HeldDialog> NextAsync() => await _dialogs.Reader.ReadAsync(TestCancelled).AsTask().WaitAsync(StepTimeout, TestCancelled);
    }

    private sealed class HeldDialog(GitPrompt prompt)
    {
        public GitPrompt Prompt { get; } = prompt;

        public TaskCompletionSource<PromptDialogAnswer> Answered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Answer(PromptDialogAnswer answer) => Answered.SetResult(answer);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingBroker : IGitPromptBroker
    {
        public List<RecordedScope> Scopes { get; } = [];

        public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null, IReadOnlyList<CredentialBinding>? bindings = null)
        {
            var scope = new RecordedScope(operationId ?? $"prompt-{Scopes.Count + 1}", title, repositoryName, mode) { Group = group };
            Scopes.Add(scope);
            return scope;
        }
    }

    private sealed class RecordedScope(string id, string title, string repositoryName, PromptMode mode)
        : PromptScope(id, new Dictionary<string, string?> { ["GIT_ASKPASS"] = "/helper", ["VISUALCOMMIT_OPERATION_ID"] = id })
    {
        public string Title { get; } = title;

        public string RepositoryName { get; } = repositoryName;

        public PromptMode Mode { get; } = mode;

        /// <summary>The prompt group the scope was opened with (docs/phase-6-design.md 4.4), or null.</summary>
        public PromptGroup? Group { get; init; }

        public void CancelByUser() => UserCancelled = true;
    }

    private sealed class RecordingProvider(Exception failure) : IRepositoryProvider
    {
        public IReadOnlyDictionary<string, string?>? Environment { get; private set; }

        public Action? BeforeFailing { get; set; }

        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null)
        {
            Environment = environment;
            BeforeFailing?.Invoke();
            return Task.FromException<IGitRepository>(failure);
        }
    }

    private sealed class MemorySession : VisualCommit.Core.Session.ISessionStore
    {
        public VisualCommit.Core.Session.SessionState Current { get; private set; } = new();

        public void Update(Func<VisualCommit.Core.Session.SessionState, VisualCommit.Core.Session.SessionState> change) => Current = change(Current);
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
