using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The settings screen (<c>SettingsDialog</c>, docs/phase-6-design.md 7.1): its first focus is
/// the shown section's button, never a text box; Up and Down move between the section buttons;
/// the shown section starts at its top; a field the view model names takes the focus (the first
/// wrong one after a refused Save, Other…'s program box). The Git program row's Browse…, disabled
/// while its file dialog is open and its file is probed, takes the focus back once its run has
/// ended with the focus lost, as by Tab when it had the keyboard outline as it was pressed; it also
/// takes the focus, as by Tab, from the row's "Use the Git on the PATH" when that button, pressed
/// with the keyboard outline, hides itself with the choice it drops.
/// While the shown section is at its top, its scroller lets what is drawn just above its first row
/// show, so that a radio button there shows its whole keyboard frame.
/// </summary>
public partial class SettingsDialogView : DialogView
{
    /// <summary>
    /// How far above the section's top what its first row draws may show while the section is at its
    /// top: a radio button's keyboard frame reaches 4 above the button (<c>VcRadioButton</c>), and
    /// General's Theme options are its first row.
    /// </summary>
    private const double SectionTopReach = 4;

    /// <summary>About's image: the app's mark at 96 pixels, drawn at 48 × 48 so that it stays sharp at 200 %; decoded once.</summary>
    private static readonly Lazy<Bitmap> AboutImage = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri(AppIcon.Image96Uri));
        return new Bitmap(stream);
    });

    private SettingsDialogViewModel? _model;

    /// <summary>The section scroller's presenter, once its template is applied.</summary>
    private ScrollContentPresenter? _sectionPresenter;

    public SettingsDialogView()
    {
        InitializeComponent();
        AboutIcon.Source = AboutImage.Value;
        DataContextChanged += (_, _) => Follow(DataContext as SettingsDialogViewModel);
        SettingsNav.AddHandler(KeyDownEvent, OnNavKeyDown, RoutingStrategies.Tunnel);
        GitProgramBrowseButton.Click += OnGitProgramBrowseClick;
        FocusReturn.AfterKeyboardPressThatHides(GitProgramUsePathButton, () => GitProgramBrowseButton);
        SectionScroller.TemplateApplied += OnSectionScrollerTemplateApplied;
        SectionScroller.PropertyChanged += OnSectionClipChanged;
    }

    /// <summary>
    /// The section scroller and its presenter clip to their bounds, which cut off the keyboard frame
    /// of what stands at the section's top. While the content is at its top (offset 0) both clip to
    /// their bounds grown by <see cref="SectionTopReach"/> upward instead, where nothing but such a
    /// frame is drawn (the 14 above the section are the card's); scrolled, they clip to their bounds
    /// as before, so that no row scrolled out of view shows there. Their bottoms and sides stay where
    /// their bounds end.
    /// </summary>
    private void OnSectionScrollerTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _sectionPresenter = e.NameScope.Find<ScrollContentPresenter>("PART_ContentPresenter");
        if (_sectionPresenter is { } presenter)
        {
            presenter.PropertyChanged += OnSectionClipChanged;
        }

        ClipSection();
    }

    private void OnSectionClipChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        var scroller = ReferenceEquals(sender, SectionScroller);
        var presenter = sender is not null && ReferenceEquals(sender, _sectionPresenter);
        if ((scroller || presenter) && (change.Property == BoundsProperty || (presenter && change.Property == ScrollContentPresenter.OffsetProperty)))
        {
            ClipSection();
        }
    }

    private void ClipSection()
    {
        if (_sectionPresenter is not { } presenter)
        {
            return;
        }

        var atTop = presenter.Offset.Y <= 0;
        foreach (var clipping in new Control[] { SectionScroller, presenter })
        {
            if (atTop)
            {
                clipping.ClipToBounds = false;
                clipping.Clip = new RectangleGeometry(new Rect(0, -SectionTopReach, clipping.Bounds.Width, clipping.Bounds.Height + SectionTopReach));
            }
            else
            {
                clipping.ClearValue(ClipToBoundsProperty);
                clipping.ClearValue(ClipProperty);
            }
        }
    }

    /// <summary>Runs before Browse…'s command: the focus back on the button once its run has ended with the focus lost.</summary>
    private void OnGitProgramBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not { } model)
        {
            return;
        }

        var withKeys = GitProgramBrowseButton.IsFocused && GitProgramBrowseButton.Classes.Contains(":focus-visible");
        FocusReturn.AfterRun(
            model.GitProgram.BrowseCommand,
            this,
            () => GitProgramBrowseButton,
            withKeys ? NavigationMethod.Tab : NavigationMethod.Unspecified,
            FocusReturn.LostFrom(TopLevel.GetTopLevel(this), GitProgramBrowseButton, static () => true));
    }

    /// <summary>The settings screen gives its first focus to a section button, not a text box (7.1).</summary>
    protected override bool FocusesTextBoxOnOpening => false;

    /// <summary>The shown section's button.</summary>
    protected override Control? InitialFocus => NavButtonOf(_model?.SelectedSection ?? SettingsSection.General);

    private Button NavButtonOf(SettingsSection section) => section switch
    {
        SettingsSection.Git => SettingsNavGit,
        SettingsSection.Tools => SettingsNavTools,
        SettingsSection.KeyboardShortcuts => SettingsNavShortcuts,
        SettingsSection.Accounts => SettingsNavAccounts,
        SettingsSection.About => SettingsNavAbout,
        _ => SettingsNavGeneral,
    };

    private void Follow(SettingsDialogViewModel? model)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.FocusRequested -= OnFocusRequested;
        }

        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.FocusRequested += OnFocusRequested;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsDialogViewModel.SelectedSection))
        {
            // Each section starts at its top.
            SectionScroller.Offset = default;
        }
    }

    /// <summary>Focuses the control <paramref name="automationId"/> once the layout shows it (the section may just have changed).</summary>
    private void OnFocusRequested(object? sender, string automationId) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                var control = this.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(candidate => AutomationProperties.GetAutomationId(candidate) == automationId && candidate.IsEffectivelyVisible);
                if (control is null)
                {
                    return;
                }

                control.Focus(NavigationMethod.Tab);
                control.BringIntoView();
                if (control is TextBox box)
                {
                    // The caret at the end and nothing selected, as a dialog's box on opening: the
                    // Tab focus selects the whole text, which a caret already at the end leaves
                    // (clearing the selection puts the caret at its start, so it comes first).
                    box.ClearSelection();
                    box.CaretIndex = box.Text?.Length ?? 0;
                }
            },
            DispatcherPriority.Loaded);

    /// <summary>Up and Down move between the section buttons (7.1); the section shows on a click or Enter only.</summary>
    private void OnNavKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        var buttons = SettingsNav.Children.OfType<Button>().ToList();
        var index = buttons.FindIndex(button => button.IsFocused);
        if (index < 0)
        {
            return;
        }

        var next = e.Key == Key.Up ? Math.Max(0, index - 1) : Math.Min(buttons.Count - 1, index + 1);
        buttons[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
