using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the platform's folder dialog in the scripted walk-through, which cannot drive a
/// native dialog (D42): it answers each request with the next folder the test queued, or as a
/// cancelled dialog when none is queued. The real-window pass drives the real dialog instead.
/// </summary>
public sealed class FakeFolderPicker : IFolderPicker
{
    private readonly Queue<string?> _answers = new();
    private readonly List<string> _titles = [];

    /// <summary>The titles of the dialogs the app asked for, in order.</summary>
    public IReadOnlyList<string> Titles => _titles;

    /// <summary>Queues the folder the next dialog "chooses"; null makes it a cancelled dialog.</summary>
    public FakeFolderPicker Answer(string? folder)
    {
        _answers.Enqueue(folder);
        return this;
    }

    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        _titles.Add(title);
        return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }
}
