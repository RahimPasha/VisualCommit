using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Pages;

/// <summary>
/// What the graph area's pages share (docs/phase-5-design.md 11.1, D176): the host, a lifetime
/// that cancels every read of the page when it closes or its tab does, the title that names its
/// pane, the focus request and the close. Reads go straight to the repository, outside the queue
/// (D47); nothing here writes. It does not touch Avalonia: what a read hands over on another
/// thread comes back through the synchronization context the page was made on.
/// </summary>
public abstract partial class GraphAreaPageViewModel : ObservableObject, IGraphAreaPage
{
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _token;
    private readonly SynchronizationContext? _ui;
    private bool _disposed;

    protected GraphAreaPageViewModel(IGraphAreaPageHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(host.Lifetime);
        _token = _lifetime.Token;
        _ui = SynchronizationContext.Current;
    }

    /// <summary>The repository the page shows.</summary>
    protected IGraphAreaPageHost Host { get; }

    /// <summary>Cancelled when the page closes or its tab does.</summary>
    protected CancellationToken Lifetime => _token;

    /// <summary>The page was closed (disposed): it reads and shows nothing more.</summary>
    public bool IsClosed => _disposed;

    /// <summary>The page's title, which names its pane in UI Automation: <c>History of Parser.cs</c>.</summary>
    [ObservableProperty]
    public partial string Title { get; protected set; } = string.Empty;

    /// <inheritdoc/>
    public event EventHandler? FocusRequested;

    /// <summary>The close button and Esc: the host closes the page and the graph takes the focus.</summary>
    [RelayCommand]
    public void Close() => Host.Close(this);

    /// <inheritdoc/>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public virtual void OnRefsRead(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    /// <summary>The page closed: a page lets go of what it holds.</summary>
    protected virtual void OnDisposed()
    {
    }

    /// <summary>Runs <paramref name="action"/> on the thread the page was made on (the UI thread in the app), or at once without one (tests).</summary>
    protected void OnUiThread(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    /// <summary>The first 7 characters of a commit id, as the graph's SHA column shows it.</summary>
    protected static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
