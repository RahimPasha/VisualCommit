using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The create form per provider (docs/phase-7b-design.md 13.2, 13.4, 13.7; D351, D355, D362;
/// rulings 3 and 20): 13.2's texts per provider, GitHub's equal to 7a's constants; the title and
/// Into's reading text by <see cref="ICreatePullRequestActions.Provider"/>; GitLab's sent title while
/// Draft is ticked (<c>Draft: x</c>, <c>[Draft] x</c>, <c>(draft) x</c> and plain) and its
/// quick-actions note, never on GitHub or Azure DevOps; in the view, with 7a's names, the From and
/// Into rows' children where a copy of 7a's <c>StackPanel</c> rows puts them in the same window, and
/// a 60-character repository text cut with its whole text as its tooltip, <c>Choose…</c> inside the card.
/// </summary>
public sealed class CreateRequestDialogProvidersTests
{
    private const string LongRepository = "northfield/platform/services/billing/invoice-generation-60ch";

    public static TheoryData<HostingProvider> Providers => [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    [Fact]
    public void GitHub_s_provider_forms_are_7a_s_texts_word_for_word()
    {
        var p = HostingProvider.GitHub;
        Assert.Equal(Texts.DialogTitle, Texts.DialogTitleOf(p));
        Assert.Equal(Texts.ReadingGitHub, Texts.ReadingOf(p));
        Assert.Equal(Texts.CreatingPullRequest, Texts.CreatingRequest(p));
        Assert.Equal(Texts.Operation("topic/no-pr"), Texts.Operation(p, "topic/no-pr"));
        Assert.Equal(Texts.Created(6, false), Texts.Created(p, 6, false));
        Assert.Equal(Texts.Created(6, true), Texts.Created(p, 6, true));
        Assert.Equal(Texts.OpenExisting(1), Texts.OpenExisting(p, 1));
        Assert.Equal(Texts.AlreadyOpen(1, "topic/no-pr", "origin/main"), Texts.AlreadyOpen(p, 1, "topic/no-pr", "origin/main"));
        Assert.Equal(Texts.PushedFirstWithUpstream("origin/local-work"), Texts.PushedFirstWithUpstream(p, "origin/local-work"));
        Assert.Equal(Texts.PushedFirst("origin/local-work"), Texts.PushedFirst(p, "origin/local-work"));
        Assert.Equal(Texts.AheadPushedFirst(1, "origin/x"), Texts.AheadPushedFirst(p, 1, "origin/x"));
        Assert.Equal(Texts.AheadPushedFirst(3, "origin/x"), Texts.AheadPushedFirst(p, 3, "origin/x"));
        Assert.Equal(Texts.BaseUnknown("main", "octo/hello"), Texts.BaseUnknown(p, "main", "octo/hello"));
        Assert.Equal(Texts.NotVisible("octo/hello"), Texts.NotVisible(p, "octo/hello"));
        Assert.Equal(Texts.Cancelled, Texts.CancelledOf(p));
        Assert.Equal(Texts.Refused("octo/hello", false), Texts.Refused(p, "octo/hello", false));
        Assert.Equal(Texts.Refused("octo/hello", true), Texts.Refused(p, "octo/hello", true));
        Assert.Equal("Create pull request", Texts.DialogTitle);
        Assert.Equal("Created draft pull request #6", Texts.Created(p, 6, true));
    }

    [Fact]
    public void GitLab_s_and_Azure_DevOps_texts_name_their_provider_request_and_number()
    {
        var gl = HostingProvider.GitLab;
        var az = HostingProvider.AzureDevOps;
        Assert.Equal(("Create merge request", "Create pull request"), (Texts.DialogTitleOf(gl), Texts.DialogTitleOf(az)));
        Assert.Equal(("Reading GitLab…", "Reading Azure DevOps…"), (Texts.ReadingOf(gl), Texts.ReadingOf(az)));
        Assert.Equal(("Creating merge request…", "Creating pull request…"), (Texts.CreatingRequest(gl), Texts.CreatingRequest(az)));
        Assert.Equal(
            new OperationText("Create merge request for topic/no-request", "Creating merge request…", "Create merge request failed", "GitLab could not create the merge request."),
            Texts.Operation(gl, "topic/no-request"));
        Assert.Equal(
            new OperationText("Create pull request for topic/no-request", "Creating pull request…", "Create pull request failed", "Azure DevOps could not create the pull request."),
            Texts.Operation(az, "topic/no-request"));
        Assert.Equal(("Created merge request !24", "Created draft merge request !24"), (Texts.Created(gl, 24, false), Texts.Created(gl, 24, true)));
        Assert.Equal(("Created pull request !14", "Created draft pull request !14"), (Texts.Created(az, 14, false), Texts.Created(az, 14, true)));
        Assert.Equal(("Open merge request !24", "Open pull request !14"), (Texts.OpenExisting(gl, 24), Texts.OpenExisting(az, 14)));
        Assert.Equal("Merge request !24 is already open from topic/no-request into origin/main.", Texts.AlreadyOpen(gl, 24, "topic/no-request", "origin/main"));
        Assert.Equal("Pull request !14 is already open from topic/no-request into origin/main.", Texts.AlreadyOpen(az, 14, "topic/no-request", "origin/main"));
        Assert.Equal("Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.", Texts.PushedFirstWithUpstream(gl, "origin/local-work"));
        Assert.Equal("Not on Azure DevOps yet: Create pushes it to origin/local-work first and sets that as its upstream.", Texts.PushedFirstWithUpstream(az, "origin/local-work"));
        Assert.Equal("Not on GitLab yet: Create pushes it to origin/local-work first.", Texts.PushedFirst(gl, "origin/local-work"));
        Assert.Equal("1 commit is not on Azure DevOps yet: Create pushes it to origin/x first.", Texts.AheadPushedFirst(az, 1, "origin/x"));
        Assert.Equal("2 commits are not on GitLab yet: Create pushes them to origin/x first.", Texts.AheadPushedFirst(gl, 2, "origin/x"));
        Assert.Equal("GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.", Texts.HeadUnknown(gl, "topic/no-request", "northfield/platform/web-app"));
        Assert.Equal("Azure DevOps cannot find topic/no-request or main. Fetch, then try again.", Texts.BranchesUnknown(az, "topic/no-request", "main"));
        Assert.Equal("GitLab has no branch main in northfield/platform/web-app. Choose another branch to merge into.", Texts.BaseUnknown(gl, "main", "northfield/platform/web-app"));
        Assert.Equal("This account cannot see northfield/platform/web-app on GitLab.", Texts.NotVisible(gl, "northfield/platform/web-app"));
        Assert.Equal(("Cancelled before the merge request was created.", "Cancelled before the pull request was created."), (Texts.CancelledOf(gl), Texts.CancelledOf(az)));
        Assert.Equal("GitLab refused to create a merge request in northfield/platform/web-app with this token.", Texts.Refused(gl, "northfield/platform/web-app", false));
        Assert.Equal("GitLab refused to create a merge request in northfield/platform/web-app with this token. It needs the api scope, or Merge Request: Create.", Texts.Refused(gl, "northfield/platform/web-app", true));
        Assert.Equal("Azure DevOps refused to create a pull request in Fernhill-Labs/Mobile App/mobile-app with this token.", Texts.Refused(az, "Fernhill-Labs/Mobile App/mobile-app", true));
        Assert.Equal("Title on GitLab: Draft: Local change", Texts.SentTitle(gl, "Draft: Local change"));
        Assert.Equal("GitLab runs quick actions in the description, such as /close.", Texts.QuickActionsNote);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_title_and_Into_s_reading_text_are_the_provider_s(HostingProvider provider)
    {
        var actions = new FakeActions { Provider = provider };
        var form = new CreatePullRequestDialogViewModel(actions);
        Assert.Equal(Texts.DialogTitleOf(provider), form.Title);
        Assert.Equal(provider, form.Provider);
        Assert.True(form.IsReadingBase);
        Assert.Equal(Texts.ReadingOf(provider), form.BaseText);
        Assert.Equal(provider == HostingProvider.GitLab ? "Reading GitLab…" : provider == HostingProvider.GitHub ? "Reading GitHub…" : "Reading Azure DevOps…", form.BaseText);

        actions.Start.SetResult(new CreatePullRequestStart(null, null));
        await form.Starting;
        actions.Create.SetResult(new CreatePullRequestOutcome(false, "No.", 24));
        form.PullRequestTitle = "Try an idea";
        form.ChooseBaseCommand.Execute(null);
        Assert.Equal("origin/main", form.BaseText);
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal(Texts.OpenExisting(provider, 24), form.OpenExistingText);
    }

    [Theory]
    [InlineData("Draft: x", "Title on GitLab: Draft: x")]
    [InlineData("[Draft] x", "Title on GitLab: [Draft] x")]
    [InlineData("(draft) x", "Title on GitLab: (draft) x")]
    [InlineData("Add the search box", "Title on GitLab: Draft: Add the search box")]
    [InlineData("Add the search box   ", "Title on GitLab: Draft: Add the search box")]
    public void GitLab_s_sent_title_shows_while_Draft_is_ticked_by_the_provider_s_rule(string title, string sent)
    {
        var form = new CreatePullRequestDialogViewModel(new FakeActions { Provider = HostingProvider.GitLab, Given = Main }) { PullRequestTitle = title };
        Assert.False(form.ShowsSentTitle);
        Assert.True(form.ShowsQuickActionsNote);
        Assert.Equal("GitLab runs quick actions in the description, such as /close.", form.QuickActionsNote);

        var changed = new List<string?>();
        form.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        form.IsDraft = true;
        Assert.True(form.ShowsSentTitle);
        Assert.Contains(nameof(form.ShowsSentTitle), changed);
        Assert.Equal(sent, form.SentTitle);
        Assert.Equal("Title on GitLab: " + VisualCommit.Hosting.GitLab.GitLabProvider.TitleFor(title.TrimEnd(), true), form.SentTitle);

        form.PullRequestTitle = "Other";
        Assert.Contains(nameof(form.SentTitle), changed);
        Assert.Equal("Title on GitLab: Draft: Other", form.SentTitle);
        form.IsDraft = false;
        Assert.False(form.ShowsSentTitle);
    }

    [Theory]
    [InlineData(HostingProvider.GitHub)]
    [InlineData(HostingProvider.AzureDevOps)]
    public void Neither_GitLab_line_shows_on_GitHub_or_Azure_DevOps(HostingProvider provider)
    {
        var form = new CreatePullRequestDialogViewModel(new FakeActions { Provider = provider, Given = Main }) { PullRequestTitle = "Draft: x" };
        foreach (var draft in new[] { false, true })
        {
            form.IsDraft = draft;
            Assert.False(form.ShowsSentTitle);
            Assert.False(form.ShowsQuickActionsNote);
        }
    }

    [AvaloniaFact]
    public async Task With_7a_s_names_the_rows_place_each_child_where_7a_s_StackPanels_did()
    {
        using var data = new TempDirectory("create form rows");
        using var app = ShellDriver.Start(data.Path);
        var view = await ShowAsync(app, new FakeActions
        {
            HeadEnd = new PullRequestEnd("topic/no-pr", "octo/hello"),
            Given = new PullRequestEnd("origin/main", "octo/hello") { Branch = "main" },
        });

        foreach (var (row, names) in new[]
        {
            (Row(app, view, "PullRequestHead"), new[] { "PullRequestHead", "PullRequestHeadRepository" }),
            (Row(app, view, "PullRequestBase"), new[] { "PullRequestBase", "PullRequestBaseRepository", "PullRequestBaseChooseButton" }),
        })
        {
            Assert.IsType<DockPanel>(row);
            Assert.Equal(HorizontalAlignment.Left, row.HorizontalAlignment);
            // The copy goes where the row is: over it in the Into row's Grid cell, below the From row
            // in its column's StackPanel; both have the row's width to lay out in.
            var copy = StackPanelCopy(app, view, names);
            var parent = (Panel)row.Parent!;
            Grid.SetRow(copy, Grid.GetRow(row));
            Grid.SetColumn(copy, Grid.GetColumn(row));
            copy.Margin = row.Margin;
            copy.Opacity = 0;
            parent.Children.Add(copy);
            app.Settle();
            try
            {
                var rowOrigin = app.BoundsOf(row).Position;
                var copyOrigin = app.BoundsOf(copy).Position;
                Assert.Equal(rowOrigin.X, copyOrigin.X, 0.5);
                for (var i = 0; i < names.Length; i++)
                {
                    var built = app.BoundsOf(app.FindByAutomationId(view, names[i])).Translate(-(Vector)rowOrigin);
                    var seven = app.BoundsOf(copy.Children[i]).Translate(-(Vector)copyOrigin);
                    Assert.True(Near(built, seven), $"{names[i]}: {built} where 7a's StackPanel puts {seven}");
                }
            }
            finally
            {
                parent.Children.Remove(copy);
                app.Settle();
            }
        }

        foreach (var name in new[] { "PullRequestHeadRepository", "PullRequestBaseRepository" })
        {
            var text = (TextBlock)app.FindByAutomationId(view, name);
            Assert.Equal("octo/hello", ToolTip.GetTip(text));
            Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, text.TextTrimming);
            Assert.DoesNotContain(text.TextLayout.TextLines, line => line.HasCollapsed);
        }

        foreach (var name in new[] { "PullRequestQuickActionsNote", "PullRequestSentTitle" })
        {
            Assert.False(app.FindByAutomationId(view, name).IsVisible);
        }
    }

    [AvaloniaFact]
    public async Task A_60_character_repository_is_cut_with_its_whole_text_its_tooltip_and_Choose_inside_the_card()
    {
        Assert.Equal(60, LongRepository.Length);
        using var data = new TempDirectory("create form long");
        using var app = ShellDriver.Start(data.Path);
        var view = await ShowAsync(app, new FakeActions
        {
            Provider = HostingProvider.GitLab,
            HeadEnd = new PullRequestEnd("topic/no-request", LongRepository),
            Given = new PullRequestEnd("origin/main", LongRepository) { Branch = "main" },
        });

        var card = app.BoundsOf(view.GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard"));
        var column = app.BoundsOf(Row(app, view, "PullRequestHead").Parent is StackPanel from ? from : Row(app, view, "PullRequestHead"));
        foreach (var name in new[] { "PullRequestHeadRepository", "PullRequestBaseRepository" })
        {
            var text = (TextBlock)app.FindByAutomationId(view, name);
            Assert.Equal(LongRepository, text.Text);
            Assert.Equal(LongRepository, ToolTip.GetTip(text));
            Assert.True(text.TextLayout.TextLines.Single().HasCollapsed, $"{name} is not cut");
            Assert.True(app.BoundsOf(text).Right <= column.Right + 1, $"{name} passes the column");
        }

        var choose = (Button)app.FindByAutomationId(view, "PullRequestBaseChooseButton");
        var chooseBounds = app.BoundsOf(choose);
        Assert.Equal(82, chooseBounds.Width, 1.0);
        Assert.True(chooseBounds.Right <= card.Right - 20, $"Choose… at {chooseBounds} leaves the card {card}");
        Assert.Equal(column.Right, chooseBounds.Right, 1.0);
        Assert.True(app.FindByAutomationId(view, "PullRequestQuickActionsNote").IsVisible);
        Assert.Empty(LayoutAudit.FindClippedText(TopLevel.GetTopLevel(view)!, allowShortenedWithToolTip: true));
    }

    [AvaloniaFact]
    public async Task GitLab_s_two_lines_take_their_places_and_push_what_is_below_them_down()
    {
        using var data = new TempDirectory("create form lines");
        using var app = ShellDriver.Start(data.Path);
        var azure = await ShowAsync(app, new FakeActions { Provider = HostingProvider.AzureDevOps, Given = Main });
        var places = Places(app, azure);
        app.Session.Services.DialogHost.CancelAll();
        app.Settle();

        var actions = new FakeActions { Provider = HostingProvider.GitLab, Given = Main };
        var view = await ShowAsync(app, actions);
        var form = (CreatePullRequestDialogViewModel)view.DataContext!;
        var note = (TextBlock)app.FindByAutomationId(view, "PullRequestQuickActionsNote");
        var description = app.BoundsOf(app.FindByAutomationId(view, "PullRequestDescription"));
        var noteBounds = app.BoundsOf(note);
        Assert.Equal("GitLab runs quick actions in the description, such as /close.", note.Text);
        Assert.Contains("settingsNote", note.Classes);
        Assert.Equal(description.Bottom + 4, noteBounds.Top, 1.0);
        Assert.Single(note.TextLayout.TextLines);
        var shift = noteBounds.Height + 4;
        var gitLab = Places(app, view);
        foreach (var name in new[] { "CreatePullRequestDialogTitle", "PullRequestHead", "PullRequestBase", "PullRequestTitle", "PullRequestDescription" })
        {
            Assert.Equal(places[name].Y, gitLab[name].Y, 1.0);
        }

        foreach (var name in new[] { "PullRequestDraft", "CancelButton", "ConfirmButton" })
        {
            Assert.Equal(places[name].Y + shift, gitLab[name].Y, 1.0);
        }

        Assert.Equal(places["CreatePullRequestDialogCard"].Height + shift, gitLab["CreatePullRequestDialogCard"].Height, 1.0);

        form.PullRequestTitle = "Local change";
        form.IsDraft = true;
        app.Settle();
        var sent = (TextBlock)app.FindByAutomationId(view, "PullRequestSentTitle");
        Assert.True(sent.IsEffectivelyVisible);
        Assert.Equal("Title on GitLab: Draft: Local change", sent.Text);
        Assert.Contains("settingsNote", sent.Classes);
        Assert.Equal(app.BoundsOf(app.FindByAutomationId(view, "PullRequestDraft")).Bottom + 4, app.BoundsOf(sent).Top, 1.0);
        var ticked = Places(app, view);
        var shift2 = app.BoundsOf(sent).Height + 4;
        Assert.Equal(gitLab["PullRequestDraft"].Y, ticked["PullRequestDraft"].Y, 1.0);
        Assert.Equal(gitLab["ConfirmButton"].Y + shift2, ticked["ConfirmButton"].Y, 1.0);
        Assert.Equal(gitLab["CreatePullRequestDialogCard"].Height + shift2, ticked["CreatePullRequestDialogCard"].Height, 1.0);
        Assert.Equal("Local change", form.PullRequestTitle);
    }

    private static readonly PullRequestEnd Main = new("origin/main", "northfield/platform/web-app") { Branch = "main" };

    private static Dictionary<string, Rect> Places(ShellDriver app, Control view)
    {
        var card = view.GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard");
        var origin = app.BoundsOf(card).Position;
        var places = new Dictionary<string, Rect>(StringComparer.Ordinal) { ["CreatePullRequestDialogCard"] = new Rect(0, 0, card.Bounds.Width, card.Bounds.Height) };
        foreach (var name in new[] { "CreatePullRequestDialogTitle", "PullRequestHead", "PullRequestBase", "PullRequestTitle", "PullRequestDescription", "PullRequestDraft", "CancelButton", "ConfirmButton" })
        {
            var bounds = app.BoundsOf(app.FindByAutomationId(view, name));
            places[name] = new Rect(bounds.X - origin.X, bounds.Y - origin.Y, bounds.Width, bounds.Height);
        }

        return places;
    }

    private static bool Near(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) <= 0.5 && Math.Abs(a.Y - b.Y) <= 0.5 && Math.Abs(a.Width - b.Width) <= 0.5 && Math.Abs(a.Height - b.Height) <= 0.5;

    private static Panel Row(ShellDriver app, Control view, string firstChild) => (Panel)app.FindByAutomationId(view, firstChild).Parent!;

    /// <summary>A copy of 7a's row (CreatePullRequestDialogView.axaml at 7a's gate): a horizontal StackPanel, Spacing 8, of the same controls.</summary>
    private static StackPanel StackPanelCopy(ShellDriver app, Control view, string[] names)
    {
        var copy = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var name in names)
        {
            var built = app.FindByAutomationId(view, name);
            Control child = built switch
            {
                Button button => new Button { Theme = button.Theme, Content = button.Content },
                TextBlock text => new TextBlock { Text = text.Text, MaxWidth = text.MaxWidth, VerticalAlignment = text.VerticalAlignment },
                _ => throw new InvalidOperationException(name),
            };
            if (built is TextBlock block)
            {
                foreach (var cls in block.Classes)
                {
                    child.Classes.Add(cls);
                }

                ((TextBlock)child).TextTrimming = name is "PullRequestHead" or "PullRequestBase" ? Avalonia.Media.TextTrimming.CharacterEllipsis : Avalonia.Media.TextTrimming.None;
            }

            copy.Children.Add(child);
        }

        return copy;
    }

    private static async Task<Control> ShowAsync(ShellDriver app, FakeActions actions)
    {
        var form = new CreatePullRequestDialogViewModel(actions);
        _ = app.Session.Services.DialogHost.ShowAsync(form);
        await app.WaitForAsync(() => app.TopDialogView is CreatePullRequestDialogView, "the form");
        app.Settle();
        var view = app.TopDialogView!;
        Assert.Equal(form.Title, AutomationProperties.GetName(view));
        return view;
    }

    private sealed class FakeActions : ICreatePullRequestActions
    {
        public HostingProvider Provider { get; init; } = HostingProvider.GitHub;

        public PullRequestEnd HeadEnd { get; init; } = new("topic/no-request", "northfield/platform/web-app");

        public PullRequestEnd? Given { get; init; }

        public PullRequestEnd Head => HeadEnd;

        public string DefaultTitle => "Try an idea";

        public PullRequestEnd? GivenBase => Given;

        public TaskCompletionSource<CreatePullRequestStart> Start { get; } = new();

        public TaskCompletionSource<CreatePullRequestOutcome> Create { get; } = new();

        public bool CanCancelCreate => false;

        public event EventHandler? CanCancelCreateChanged
        {
            add { }
            remove { }
        }

        public string? HeadNote() => null;

        public Task<CreatePullRequestStart> StartAsync(CancellationToken ct) => Start.Task;

        public Task<PullRequestEnd?> ChooseBaseAsync(PullRequestEnd? current) => Task.FromResult<PullRequestEnd?>(Main);

        public bool IsSameBranch(PullRequestEnd baseEnd) => false;

        public Task<CreatePullRequestOutcome> CreateAsync(PullRequestEnd baseEnd, string title, string body, bool draft) => Create.Task;

        public Task OpenExistingAsync() => Task.CompletedTask;

        public void CancelCreate()
        {
        }
    }
}
