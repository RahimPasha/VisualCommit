using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The command palette (D191, D212; docs/phase-6-design.md 5.2): a dialog of the dialog layer,
/// without dimming, whose answer is the entry to run (null when cancelled). Its box filters the
/// commands and the items by <see cref="PaletteMatcher"/>; the highlighted entry runs on Enter,
/// after the palette has closed (5.9). The layer gives it Esc, the focus on opening and back on
/// closing, and its UI Automation pane (<see cref="Title"/>).
/// </summary>
public sealed partial class CommandPaletteViewModel : DialogViewModel<PaletteEntry?>
{
    /// <summary>The palette's title: its UI Automation pane's name; no title is drawn (5.2).</summary>
    public const string PaletteTitle = "Command palette";

    /// <summary>How far PageUp and PageDown move the highlight (5.8): 12 rows show, a page keeps one.</summary>
    public const int PageRows = 11;

    private readonly IReadOnlyList<PaletteEntry> _commands;
    private readonly IReadOnlyList<string> _recent;
    private IReadOnlyList<PaletteEntry> _items;
    private int _highlighted = -1;

    /// <param name="commands">The commands (5.4), in source order.</param>
    /// <param name="items">The items (5.5), in source order.</param>
    /// <param name="recent">The <see cref="PaletteEntry.Key"/>s run this session, newest first (5.6).</param>
    /// <param name="query">The box's starting text (6.5's <c>workspace</c>), the caret at its end.</param>
    public CommandPaletteViewModel(IReadOnlyList<PaletteEntry> commands, IReadOnlyList<PaletteEntry> items, IReadOnlyList<string> recent, string query = "")
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(query);
        _commands = commands;
        _items = items;
        _recent = recent;
        Query = query;
        if (query.Length == 0)
        {
            // The partial property's change hook does not run for its default value.
            Refill(keepKey: null);
        }
    }

    public override string Title => PaletteTitle;

    /// <summary>The box's text (two-way): every change ranks again and highlights the first match (5.6).</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>The matches in rank order, refilled with one reset per query (5.2).</summary>
    public RowCollection<PaletteEntry> Shown { get; } = [];

    /// <summary>The highlighted entry, or null when nothing matches.</summary>
    public PaletteEntry? Highlighted => _highlighted >= 0 && _highlighted < Shown.Count ? Shown[_highlighted] : null;

    /// <summary>
    /// The highlighted entry's place in <see cref="Shown"/>, or -1 when nothing matches: two-way
    /// with the list's selection, so that the pointer moves it (5.8). A place outside the list
    /// (the list's own -1 while it refills) changes nothing.
    /// </summary>
    public int HighlightedIndex
    {
        get => Highlighted is null ? -1 : _highlighted;
        set
        {
            if (value >= 0 && value < Shown.Count && value != _highlighted)
            {
                SetHighlight(value);
            }
        }
    }

    /// <summary>Nothing matches: <c>No matches</c> shows in place of the list (5.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsList))]
    public partial bool ShowsNothing { get; private set; }

    /// <summary>The list shows: something matches.</summary>
    public bool ShowsList => !ShowsNothing;

    /// <summary>What shows while nothing matches (the search bar's text).</summary>
    public static string NoMatchesText => SearchViewModel.NoMatchesText;

    /// <summary>The box's placeholder (5.3, 14.7).</summary>
    public static string Placeholder => "Search commands, branches, tags, stashes, remotes, repositories and workspaces";

    /// <summary>Raised when Mod+Shift+P comes while the palette is the top dialog (6.3): the view selects the box's text.</summary>
    public event EventHandler? SelectQueryRequested;

    protected override PaletteEntry? CancelledResult => null;

    /// <summary>
    /// Moves the highlight by <paramref name="rows"/> (5.8): Up and Down by one, wrapping first to
    /// last and back; PageUp and PageDown by <see cref="PageRows"/>, stopping at the ends.
    /// </summary>
    public void MoveHighlight(int rows, bool wrap)
    {
        if (Shown.Count == 0)
        {
            return;
        }

        var from = HighlightedIndex < 0 ? 0 : HighlightedIndex;
        var to = from + rows;
        to = wrap ? ((to % Shown.Count) + Shown.Count) % Shown.Count : Math.Clamp(to, 0, Shown.Count - 1);
        SetHighlight(to);
    }

    /// <summary>Enter (5.8): runs the highlighted entry when it is enabled; a disabled one does nothing and the palette stays.</summary>
    public void RunHighlighted()
    {
        if (Highlighted is { } entry)
        {
            Run(entry);
        }
    }

    /// <summary>Closes the palette with <paramref name="entry"/> as its answer, when it is enabled (5.7, 5.9); a disabled one does nothing.</summary>
    public void Run(PaletteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsEnabled && !IsClosed)
        {
            Complete(entry);
        }
    }

    /// <summary>
    /// The items again (5.5), after the worktrees' read found other worktrees than the items were
    /// built with: the query is kept, the highlight stays on its <see cref="PaletteEntry.Key"/>
    /// while that is shown, else on the first.
    /// </summary>
    public void ReplaceItems(IReadOnlyList<PaletteEntry> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        Refill(keepKey: Highlighted?.Key);
    }

    /// <summary>Mod+Shift+P while the palette is the top dialog (6.3, KD20): the view selects the box's whole text.</summary>
    public void RequestSelectQuery() => SelectQueryRequested?.Invoke(this, EventArgs.Empty);

    partial void OnQueryChanged(string value) => Refill(keepKey: null);

    private void Refill(string? keepKey)
    {
        var ranked = PaletteMatcher.Rank(Query, _commands, _items, _recent);
        _highlighted = -1;
        Shown.ReplaceAll(ranked);
        ShowsNothing = ranked.Count == 0;
        var kept = -1;
        if (keepKey is not null)
        {
            for (var i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].Key == keepKey)
                {
                    kept = i;
                    break;
                }
            }
        }

        SetHighlight(kept >= 0 ? kept : ranked.Count > 0 ? 0 : -1);
    }

    private void SetHighlight(int index)
    {
        _highlighted = index;
        OnPropertyChanged(nameof(HighlightedIndex));
        OnPropertyChanged(nameof(Highlighted));
    }
}
