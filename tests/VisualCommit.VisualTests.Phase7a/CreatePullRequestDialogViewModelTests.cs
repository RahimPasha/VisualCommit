using VisualCommit.App.ViewModels.Dialogs;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The "Create pull request" form's view model (docs/phase-7a-design.md 12.4, 12.7; D318; ruling
/// 59, design 17 item 8) against a fake <see cref="ICreatePullRequestActions"/>: its defaults, the
/// loading state and a base chosen meanwhile, the disabled reasons, the same branch, creating
/// (every control disabled, Esc ignored, Cancel only within the cancel window), a failure's words
/// and the note read again, the existing pull request's button, edits clearing the error, and
/// Cancel cancelling the start.
/// </summary>
public class CreatePullRequestDialogViewModelTests
{
    private static readonly PullRequestEnd Main = new("origin/main", "octo/hello") { Branch = "main" };

    private static readonly PullRequestEnd Topic = new("origin/topic/no-pr", "octo/hello") { Branch = "topic/no-pr" };

    [Fact]
    public void A_given_base_is_shown_at_once_with_the_head_its_note_and_the_default_title()
    {
        var actions = new FakeActions { GivenBase = Main, Note = "Not on GitHub yet: Create pushes it to origin/local-work first and sets that as its upstream." };
        var form = new CreatePullRequestDialogViewModel(actions);

        Assert.Equal(Texts.DialogTitle, form.Title);
        Assert.Equal(new PullRequestEnd("local-work", "octo/hello"), form.Head);
        Assert.Equal(actions.Note, form.HeadNote);
        Assert.True(form.HasHeadNote);
        Assert.Equal(("origin/main", "octo/hello", false), (form.BaseText, form.BaseRepository, form.IsReadingBase));
        Assert.Equal("Local change", form.PullRequestTitle);
        Assert.Equal((string.Empty, false), (form.Description, form.IsDraft));
        Assert.Equal((null, false, null, false), (form.Error, form.HasError, form.ExistingNumber, form.HasExisting));
        Assert.Equal((Texts.Create, (string?)null), (form.CreateText, form.CreateToolTip));
        Assert.True(form.CreateCommand.CanExecute(null));
        Assert.True(form.ChooseBaseCommand.CanExecute(null));
        Assert.True(form.CancelFormCommand.CanExecute(null));
        Assert.True(form.IsIdle);
        Assert.Equal(0, actions.Starts);
        Assert.True(form.Starting.IsCompleted);
    }

    [Fact]
    public async Task Without_a_given_base_Into_reads_GitHub_until_the_start_ends_and_a_base_chosen_meanwhile_wins()
    {
        var actions = new FakeActions();
        var form = new CreatePullRequestDialogViewModel(actions);

        Assert.True(form.IsReadingBase);
        Assert.Equal((Texts.ReadingGitHub, string.Empty), (form.BaseText, form.BaseRepository));
        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.Equal(Texts.ChooseBase, form.CreateToolTip);

        actions.Start.SetResult(new CreatePullRequestStart(Main, null));
        await form.Starting;
        Assert.Equal(("origin/main", "octo/hello", false), (form.BaseText, form.BaseRepository, form.IsReadingBase));
        Assert.True(form.CreateCommand.CanExecute(null));

        var second = new FakeActions { Chosen = Topic };
        var other = new CreatePullRequestDialogViewModel(second);
        await other.ChooseBaseCommand.ExecuteAsync(null);
        Assert.Equal(Topic, other.Base);
        Assert.False(other.IsReadingBase);
        second.Start.SetResult(new CreatePullRequestStart(Main, null));
        await other.Starting;
        Assert.Equal(Topic, other.Base);
    }

    [Fact]
    public async Task A_failed_start_leaves_Into_empty_with_the_failure_on_the_error_line()
    {
        var actions = new FakeActions();
        var form = new CreatePullRequestDialogViewModel(actions);
        actions.Start.SetResult(new CreatePullRequestStart(null, "Could not reach GitHub."));
        await form.Starting;

        Assert.Equal((string.Empty, false), (form.BaseText, form.IsReadingBase));
        Assert.Equal("Could not reach GitHub.", form.Error);
        Assert.Equal(Texts.ChooseBase, form.CreateToolTip);
        Assert.False(form.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void A_blank_title_disables_Create_with_its_reason_and_text_brings_it_back()
    {
        var form = new CreatePullRequestDialogViewModel(new FakeActions { GivenBase = Main });

        form.PullRequestTitle = "   ";
        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.Equal(Texts.EnterTitle, form.CreateToolTip);
        Assert.Null(form.Error);

        form.PullRequestTitle = "Add the docs folder";
        Assert.True(form.CreateCommand.CanExecute(null));
        Assert.Null(form.CreateToolTip);
    }

    [Fact]
    public async Task The_same_branch_disables_Create_and_says_so_until_another_base_is_chosen()
    {
        var actions = new FakeActions { HeadEnd = new PullRequestEnd("main", "octo/hello"), GivenBase = Main, Chosen = Topic };
        actions.SameAs = Main;
        var form = new CreatePullRequestDialogViewModel(actions);

        Assert.Equal("Choose a branch other than main to merge into.", form.Error);
        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.True(form.ChooseBaseCommand.CanExecute(null));

        form.PullRequestTitle = "Another title";
        Assert.Equal("Choose a branch other than main to merge into.", form.Error);

        await form.ChooseBaseCommand.ExecuteAsync(null);
        Assert.Equal(Topic, form.Base);
        Assert.Null(form.Error);
        Assert.True(form.CreateCommand.CanExecute(null));
        Assert.Equal([(PullRequestEnd?)Main], actions.ChooseCalls);
    }

    [Fact]
    public async Task Creating_disables_every_control_ignores_Esc_and_lets_Cancel_stop_the_push_only_within_the_window()
    {
        var actions = new FakeActions { GivenBase = Main };
        var form = new CreatePullRequestDialogViewModel(actions);
        form.IsDraft = true;
        form.Description = "A note for the reviewers.  \n";
        form.PullRequestTitle = "Local change  ";

        // The push runs: the window is open.
        actions.SetWindow(true);
        var creating = form.CreateCommand.ExecuteAsync(null);
        Assert.True(form.IsCreating);
        Assert.False(form.IsIdle);
        Assert.Equal(Texts.Creating, form.CreateText);
        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.False(form.ChooseBaseCommand.CanExecute(null));
        Assert.False(form.OpenExistingCommand.CanExecute(null));
        Assert.True(form.CancelFormCommand.CanExecute(null));
        Assert.Equal([("origin/main", "Local change", "A note for the reviewers.", true)], actions.Creates);

        form.HandleEscape();
        Assert.False(form.IsClosed);

        // The POST runs: the window ended (D315).
        actions.SetWindow(false);
        Assert.False(form.CancelFormCommand.CanExecute(null));
        form.CancelFormCommand.Execute(null);
        Assert.Equal(0, actions.Cancels);

        actions.SetWindow(true);
        form.CancelFormCommand.Execute(null);
        Assert.Equal(1, actions.Cancels);
        Assert.False(form.IsClosed);

        actions.SetWindow(false);
        actions.Create.SetResult(new CreatePullRequestOutcome(false, Texts.Cancelled, null));
        await creating;
        Assert.False(form.IsClosed);
        Assert.Equal(Texts.Cancelled, form.Error);
        Assert.False(form.IsCreating);
        Assert.Equal(Texts.Create, form.CreateText);
        Assert.True(form.CancelFormCommand.CanExecute(null));
        Assert.True(form.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failure_shows_its_words_reads_the_note_again_and_enables_the_controls()
    {
        var actions = new FakeActions { GivenBase = Main, Note = "Not on GitHub yet: Create pushes it to origin/local-work first and sets that as its upstream." };
        var form = new CreatePullRequestDialogViewModel(actions);
        actions.Create.SetResult(new CreatePullRequestOutcome(false, "GitHub refused to create a pull request in octo/hello with this token.", null));
        actions.Note = null;

        await form.CreateCommand.ExecuteAsync(null);

        Assert.Equal("GitHub refused to create a pull request in octo/hello with this token.", form.Error);
        Assert.Null(form.HeadNote);
        Assert.False(form.HasHeadNote);
        Assert.True(form.IsIdle);
        Assert.True(form.CreateCommand.CanExecute(null));
        Assert.False(form.IsClosed);
    }

    [Fact]
    public async Task An_open_pull_request_found_gives_its_button_which_opens_it_and_closes_the_form()
    {
        var actions = new FakeActions { GivenBase = Main };
        var form = new CreatePullRequestDialogViewModel(actions);
        actions.Create.SetResult(new CreatePullRequestOutcome(false, "Pull request #6 is already open from topic/no-pr into origin/main.", 6));

        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal((6, true, "Open pull request #6"), (form.ExistingNumber, form.HasExisting, form.OpenExistingText));

        await form.OpenExistingCommand.ExecuteAsync(null);
        Assert.Equal(1, actions.OpenedExisting);
        Assert.True(form.IsClosed);
        Assert.False(await form.Result);
    }

    [Fact]
    public async Task Changing_the_title_the_description_the_base_or_Draft_clears_the_error_and_the_button()
    {
        foreach (var change in new Action<CreatePullRequestDialogViewModel>[]
        {
            form => form.PullRequestTitle += "!",
            form => form.Description = "More.",
            form => form.IsDraft = true,
            form => form.ChooseBaseCommand.Execute(null),
        })
        {
            var actions = new FakeActions { GivenBase = Main, Chosen = Topic };
            var form = new CreatePullRequestDialogViewModel(actions);
            actions.Create.SetResult(new CreatePullRequestOutcome(false, "Pull request #6 is already open from topic/no-pr into origin/main.", 6));
            await form.CreateCommand.ExecuteAsync(null);
            Assert.True(form.HasError);

            change(form);

            Assert.Null(form.Error);
            Assert.Null(form.ExistingNumber);
            Assert.False(form.HasExisting);
        }
    }

    [Fact]
    public async Task A_create_closes_the_form_with_true_and_Cancel_before_it_cancels_the_start_with_false()
    {
        var actions = new FakeActions { GivenBase = Main };
        var form = new CreatePullRequestDialogViewModel(actions);
        actions.Create.SetResult(new CreatePullRequestOutcome(true, null, null));
        await form.CreateCommand.ExecuteAsync(null);
        Assert.True(form.IsClosed);
        Assert.True(await form.Result);

        var starting = new FakeActions();
        var other = new CreatePullRequestDialogViewModel(starting);
        Assert.False(starting.StartToken.IsCancellationRequested);
        other.CancelFormCommand.Execute(null);
        Assert.True(other.IsClosed);
        Assert.True(starting.StartToken.IsCancellationRequested);
        Assert.False(await other.Result);
        await other.Starting;
        Assert.Equal(0, starting.Cancels);

        var escaped = new CreatePullRequestDialogViewModel(new FakeActions { GivenBase = Main });
        escaped.HandleEscape();
        Assert.True(escaped.IsClosed);
        Assert.False(await escaped.Result);
    }

    private sealed class FakeActions : ICreatePullRequestActions
    {
        private bool _window;

        public PullRequestEnd HeadEnd { get; init; } = new("local-work", "octo/hello");

        public PullRequestEnd Head => HeadEnd;

        public string DefaultTitle { get; init; } = "Local change";

        public PullRequestEnd? GivenBase { get; init; }

        public string? Note { get; set; }

        public PullRequestEnd? SameAs { get; set; }

        public PullRequestEnd? Chosen { get; init; }

        public int Starts { get; private set; }

        public CancellationToken StartToken { get; private set; }

        public TaskCompletionSource<CreatePullRequestStart> Start { get; } = new();

        public TaskCompletionSource<CreatePullRequestOutcome> Create { get; } = new();

        public List<(string Base, string Title, string Body, bool Draft)> Creates { get; } = [];

        public List<PullRequestEnd?> ChooseCalls { get; } = [];

        public int Cancels { get; private set; }

        public int OpenedExisting { get; private set; }

        public bool CanCancelCreate => _window;

        public event EventHandler? CanCancelCreateChanged;

        public string? HeadNote() => Note;

        public Task<CreatePullRequestStart> StartAsync(CancellationToken ct)
        {
            Starts++;
            StartToken = ct;
            return Start.Task.WaitAsync(ct);
        }

        public Task<PullRequestEnd?> ChooseBaseAsync(PullRequestEnd? current)
        {
            ChooseCalls.Add(current);
            return Task.FromResult(Chosen);
        }

        public bool IsSameBranch(PullRequestEnd baseEnd) => SameAs is not null && baseEnd == SameAs;

        public Task<CreatePullRequestOutcome> CreateAsync(PullRequestEnd baseEnd, string title, string body, bool draft)
        {
            Creates.Add((baseEnd.Text, title, body, draft));
            return Create.Task;
        }

        public Task OpenExistingAsync()
        {
            OpenedExisting++;
            return Task.CompletedTask;
        }

        public void CancelCreate() => Cancels++;

        public void SetWindow(bool open)
        {
            _window = open;
            CanCancelCreateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
