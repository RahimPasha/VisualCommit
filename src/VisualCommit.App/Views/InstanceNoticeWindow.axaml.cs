using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;

namespace VisualCommit.App.Views;

/// <summary>
/// The notice's window (<see cref="InstanceNoticeViewModel"/>): "Try again" takes the keyboard focus
/// as a dialog's button does when the window opens and gets it back after an unanswered try; the
/// window closes itself once the notice has ended, and a close by the title bar or Alt+F4 ends the
/// notice as "Close" does. The app shows it as its only window, with no session.
/// </summary>
public partial class InstanceNoticeWindow : Window
{
    private InstanceNoticeViewModel? _notice;

    public InstanceNoticeWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as InstanceNoticeViewModel);

        // After the layout pass that shows the window: a hidden control cannot take the focus.
        Opened += (_, _) => Dispatcher.UIThread.Post(FocusTryAgain, DispatcherPriority.Loaded);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // The title bar's close and Alt+F4 are "Close"; after another end this does nothing.
        var notice = _notice;
        Follow(null);
        notice?.CloseCommand.Execute(null);
    }

    private void Follow(InstanceNoticeViewModel? notice)
    {
        if (ReferenceEquals(notice, _notice))
        {
            return;
        }

        if (_notice is not null)
        {
            _notice.Finished -= OnFinished;
            _notice.PropertyChanged -= OnNoticeChanged;
        }

        _notice = notice;
        if (notice is not null)
        {
            notice.Finished += OnFinished;
            notice.PropertyChanged += OnNoticeChanged;
        }
    }

    /// <summary>Closes the window once whoever else hears of the end has acted on it (the app starts its session first).</summary>
    private void OnFinished(object? sender, NoticeOutcome outcome) => Dispatcher.UIThread.Post(Close);

    /// <summary>An unanswered try: "Try again" keeps the focus, given back if its disabling took it.</summary>
    private void OnNoticeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstanceNoticeViewModel.IsTrying) && _notice is { IsTrying: false, Outcome: null })
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    var focused = FocusManager?.GetFocusedElement();
                    if (focused is null || ReferenceEquals(focused, InstanceNoticeTryAgain))
                    {
                        FocusTryAgain();
                    }
                },
                DispatcherPriority.Loaded);
        }
    }

    private void FocusTryAgain()
    {
        if (_notice is { Outcome: null } && InstanceNoticeTryAgain.IsEffectivelyEnabled)
        {
            InstanceNoticeTryAgain.Focus(NavigationMethod.Tab);
        }
    }
}
