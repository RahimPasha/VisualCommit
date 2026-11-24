using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the platform's folder dialog in the scripted walk-through, which cannot drive a
/// native dialog (D42): it answers each request with the next folder the test queued, or as a
/// cancelled dialog when none is queued. The real-window pass drives the real dialog instead.
/// Since phase 6 it answers the file dialog too (docs/phase-6-design.md 12.5, "Choose the editor's
/// program") from a queue of its own (<see cref="AnswerFile"/>). While <see cref="FileHold"/> is set,
/// the file dialog answers only when that task ends, as the native dialog, open until the user
/// picks or cancels, does; <see cref="FolderHold"/> holds the folder dialog so.
/// </summary>
public sealed class FakeFolderPicker : IFolderPicker
{
    private readonly Queue<string?> _answers = new();
    private readonly Queue<string?> _fileAnswers = new();
    private readonly List<string> _titles = [];
    private readonly List<string> _fileTitles = [];

    /// <summary>The titles of the folder dialogs the app asked for, in order.</summary>
    public IReadOnlyList<string> Titles => _titles;

    /// <summary>The titles of the file dialogs the app asked for, in order.</summary>
    public IReadOnlyList<string> FileTitles => _fileTitles;

    /// <summary>While set, the file dialog takes its answer from the queue when it is asked and gives it when this task ends; null answers at once.</summary>
    public Task? FileHold { get; set; }

    /// <summary>While set, the folder dialog takes its answer from the queue when it is asked and gives it when this task ends; null answers at once.</summary>
    public Task? FolderHold { get; set; }

    /// <summary>Queues the folder the next dialog "chooses"; null makes it a cancelled dialog.</summary>
    public FakeFolderPicker Answer(string? folder)
    {
        _answers.Enqueue(folder);
        return this;
    }

    /// <summary>Queues the file the next file dialog "chooses"; null makes it a cancelled dialog.</summary>
    public FakeFolderPicker AnswerFile(string? file)
    {
        _fileAnswers.Enqueue(file);
        return this;
    }

    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        _titles.Add(title);
        var answer = _answers.Count > 0 ? _answers.Dequeue() : null;
        return FolderHold is { } hold ? AfterHoldAsync(hold, answer) : Task.FromResult(answer);
    }

    public Task<string?> PickFileAsync(string title, CancellationToken cancellationToken = default)
    {
        _fileTitles.Add(title);
        var answer = _fileAnswers.Count > 0 ? _fileAnswers.Dequeue() : null;
        return FileHold is { } hold ? AfterHoldAsync(hold, answer) : Task.FromResult(answer);
    }

    private static async Task<string?> AfterHoldAsync(Task hold, string? answer)
    {
        await hold.ConfigureAwait(false);
        return answer;
    }
}
