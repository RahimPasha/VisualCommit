using System.ComponentModel;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Tests;

/// <summary>
/// Answers the dialogs a dialog layer shows, one after another as a script says, and records
/// every dialog it saw: phase 2's AnsweringDialogs for every dialog of phase 3 (confirmations,
/// the name, reset, pick, choice and push dialogs, the error dialog). Each dialog is answered as
/// soon as it becomes the top one, before the operation that opened it goes on. A dialog the
/// script has no answer for is cancelled (an error dialog closed), and a scripted answer that does
/// not fit the dialog (another type, a name the dialog refuses) is noted in <see cref="Problems"/>
/// and the dialog cancelled, so an operation never waits forever. <see cref="Keep"/> leaves a
/// dialog open for the test to act on.
/// </summary>
internal sealed class ScriptedDialogs : IDisposable
{
    private readonly DialogHostViewModel _host;
    private readonly Lock _gate = new();
    private readonly Queue<Step> _script = new();
    private readonly List<DialogViewModel> _shown = [];
    private readonly HashSet<DialogViewModel> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly List<string> _problems = [];

    public ScriptedDialogs(DialogHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        host.PropertyChanged += OnHostChanged;
    }

    /// <summary>Every dialog shown, in order.</summary>
    public IReadOnlyList<DialogViewModel> Shown
    {
        get
        {
            lock (_gate)
            {
                return [.. _shown];
            }
        }
    }

    /// <summary>The error dialogs shown, in order.</summary>
    public IReadOnlyList<ErrorDialogViewModel> Errors => [.. Shown.OfType<ErrorDialogViewModel>()];

    /// <summary>What the confirmations shown asked, in order.</summary>
    public IReadOnlyList<ConfirmationRequest> Confirmations => [.. Shown.OfType<ConfirmationViewModel>().Select(dialog => dialog.Request)];

    /// <summary>Answers that did not fit, and dialogs nothing was scripted for (other than error dialogs).</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            lock (_gate)
            {
                return [.. _problems];
            }
        }
    }

    /// <summary>The script's answers not used yet.</summary>
    public int Unused
    {
        get
        {
            lock (_gate)
            {
                return _script.Count;
            }
        }
    }

    /// <summary>The next confirmation: "yes" (its confirm button) or Cancel.</summary>
    public ScriptedDialogs Confirm(bool yes = true) =>
        Add<ConfirmationViewModel>(yes ? "confirm" : "cancel the confirmation", dialog =>
        {
            if (yes)
            {
                dialog.ConfirmCommand.Execute(null);
            }
            else
            {
                dialog.Cancel();
            }
        });

    /// <summary>
    /// The next name dialog (branch, rename, tag, stash): types <paramref name="name"/> and
    /// <paramref name="message"/> where given, sets the check box where given, and confirms; a
    /// dialog that does not let it confirm is cancelled and the reason noted.
    /// </summary>
    public ScriptedDialogs EnterName(string? name, string? message = null, bool? option = null) =>
        Add<NameDialogViewModel>($"enter \"{name}\"", dialog =>
        {
            if (name is not null)
            {
                dialog.Name = name;
            }

            if (message is not null)
            {
                dialog.Message = message;
            }

            if (option is { } isChecked)
            {
                dialog.IsOptionChecked = isChecked;
            }

            Confirm(dialog, dialog.ConfirmCommand.CanExecute(null), () => dialog.ConfirmCommand.Execute(null), dialog.NameError);
        });

    /// <summary>The next stash dialog: the message and "Include untracked files" where given, then "Stash".</summary>
    public ScriptedDialogs Stash(string message = "", bool? includeUntracked = null) => EnterName(null, message, includeUntracked);

    /// <summary>The next reset dialog: chooses <paramref name="mode"/>, then "Reset".</summary>
    public ScriptedDialogs Reset(ResetMode mode) =>
        Add<ResetDialogViewModel>($"reset {mode}", dialog =>
        {
            dialog.Mode = mode;
            dialog.ConfirmCommand.Execute(null);
        });

    /// <summary>The next pick dialog: selects <paramref name="item"/>, then confirms.</summary>
    public ScriptedDialogs Pick(string item) =>
        Add<PickDialogViewModel>($"pick {item}", dialog =>
        {
            dialog.Selected = dialog.Items.Contains(item) ? item : null;
            Confirm(dialog, dialog.Selected is not null, () => dialog.ConfirmCommand.Execute(null), $"{item} is not in the list");
        });

    /// <summary>The next remote choice dialog: checks <paramref name="option"/>, then confirms.</summary>
    public ScriptedDialogs Choose(string option) =>
        Add<ChoiceDialogViewModel>($"choose {option}", dialog =>
        {
            var chosen = dialog.Options.FirstOrDefault(candidate => candidate.Text == option);
            if (chosen is not null)
            {
                chosen.IsChecked = true;
            }

            Confirm(dialog, chosen is not null, () => dialog.ConfirmCommand.Execute(null), $"{option} is not offered");
        });

    /// <summary>The next push dialog: the remote, the branch's name and "Set as upstream" where given, then "Push".</summary>
    public ScriptedDialogs Push(string? remote = null, string? branch = null, bool? setUpstream = null) =>
        Add<PushDialogViewModel>("push", dialog =>
        {
            if (remote is not null && dialog.Remotes.FirstOrDefault(option => option.Text == remote) is { } option)
            {
                option.IsChecked = true;
            }

            if (branch is not null)
            {
                dialog.BranchName = branch;
            }

            if (setUpstream is { } set)
            {
                dialog.SetUpstream = set;
            }

            Confirm(dialog, dialog.ConfirmCommand.CanExecute(null) && (remote is null || dialog.Remote == remote), () => dialog.ConfirmCommand.Execute(null), dialog.BranchError);
        });

    /// <summary>The next error dialog: "Close", or its extra button.</summary>
    public ScriptedDialogs CloseError(bool clickExtra = false) =>
        Add<ErrorDialogViewModel>(clickExtra ? "click the extra button" : "close the error", dialog =>
        {
            if (clickExtra)
            {
                Confirm(dialog, dialog.HasExtraAction, () => dialog.ExtraCommand.Execute(null), "the dialog has no extra button");
            }
            else
            {
                dialog.ConfirmCommand.Execute(null);
            }
        });

    /// <summary>The next dialog, whatever it is: Cancel (Esc).</summary>
    public ScriptedDialogs Cancel() => Add<DialogViewModel>("cancel", dialog => dialog.Cancel());

    /// <summary>The next dialog, whatever it is: left open for the test (<see cref="WaitForAsync{T}"/>).</summary>
    public ScriptedDialogs Keep() => Add<DialogViewModel>("keep", _ => { });

    /// <summary>The next dialog, which must be a <typeparamref name="T"/>: <paramref name="answer"/> acts on it.</summary>
    public ScriptedDialogs Answer<T>(Action<T> answer)
        where T : DialogViewModel =>
        Add<T>("answer " + typeof(T).Name, answer);

    /// <summary>Waits until a <typeparamref name="T"/> has been shown (60 seconds at most) and returns the last one.</summary>
    public async Task<T> WaitForAsync<T>(TimeSpan? timeout = null)
        where T : DialogViewModel
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        while (true)
        {
            if (Shown.OfType<T>().LastOrDefault() is { } dialog)
            {
                return dialog;
            }

            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException($"No {typeof(T).Name} was shown. Shown: {string.Join(", ", Shown.Select(shown => shown.Title))}");
            }

            await Task.Delay(10);
        }
    }

    public void Dispose() => _host.PropertyChanged -= OnHostChanged;

    private ScriptedDialogs Add<T>(string description, Action<T> act)
        where T : DialogViewModel
    {
        lock (_gate)
        {
            _script.Enqueue(new Step(typeof(T), description, dialog => act((T)dialog)));
        }

        return this;
    }

    private void Confirm(DialogViewModel dialog, bool canConfirm, Action confirm, string why)
    {
        if (canConfirm)
        {
            confirm();
            return;
        }

        Note($"\"{dialog.Title}\" could not be confirmed: {why}");
        dialog.Cancel();
    }

    private void Note(string problem)
    {
        lock (_gate)
        {
            _problems.Add(problem);
        }
    }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DialogHostViewModel.Current) || _host.Current is not { } dialog)
        {
            return;
        }

        Step? answer;
        lock (_gate)
        {
            if (!_seen.Add(dialog))
            {
                // The dialog below a closed one is the top one again: it was answered already.
                return;
            }

            _shown.Add(dialog);
            _script.TryDequeue(out answer);
        }

        if (answer is null)
        {
            if (dialog is not ErrorDialogViewModel)
            {
                Note($"Nothing was scripted for \"{dialog.Title}\" ({dialog.GetType().Name}): cancelled.");
            }

            dialog.Cancel();
            return;
        }

        if (!answer.Type.IsInstanceOfType(dialog))
        {
            Note($"Expected to {answer.Description} on a {answer.Type.Name}, but \"{dialog.Title}\" ({dialog.GetType().Name}) came: cancelled.");
            dialog.Cancel();
            return;
        }

        answer.Act(dialog);
    }

    private sealed record Step(Type Type, string Description, Action<DialogViewModel> Act);
}
