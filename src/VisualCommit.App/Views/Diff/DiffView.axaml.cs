using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Views.Diff;

/// <summary>
/// The diff view (C5): see <see cref="DiffViewModel"/>. The code-behind connects what bindings
/// cannot: the images and their captions, and the diff text control's selection and hunk buttons.
/// </summary>
public partial class DiffView : UserControl
{
    /// <summary>Defines <see cref="TakesFocusOnOpen"/>.</summary>
    public static readonly StyledProperty<bool> TakesFocusOnOpenProperty =
        AvaloniaProperty.Register<DiffView, bool>(nameof(TakesFocusOnOpen), defaultValue: true);

    private DiffViewModel? _viewModel;

    public DiffView()
    {
        InitializeComponent();
        DiffText.HunkActionRequested += (_, e) => _ = _viewModel?.RunHunkActionAsync(e.Hunk, e.Action);
        DiffText.PropertyChanged += (_, e) =>
        {
            if (e.Property == DiffTextView.SelectedChangesProperty && _viewModel is not null)
            {
                _viewModel.SelectedChanges = DiffText.SelectedChanges;
            }
        };

        // The diff's menu (docs/phase-3-design.md 4b): a right-click on the text or the header, the
        // Menu key or Shift+F10. A right-press leaves the text's selection as it is (the panes
        // select with the left button only).
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    /// <summary>
    /// Whether a diff that opens, or whose text arrives, takes the keyboard focus (true, the main
    /// diff's way: Ctrl+End and the like work at once). False for the diff a file history page
    /// hosts (docs/phase-5-design.md 4.1), so that the focus stays in the page's list and Up and
    /// Down walk it.
    /// </summary>
    public bool TakesFocusOnOpen
    {
        get => GetValue(TakesFocusOnOpenProperty);
        set => SetValue(TakesFocusOnOpenProperty, value);
    }

    /// <summary>
    /// Opens the diff's menu: at the pointer, with the hunk under it when the pointer is on the
    /// text; from the keyboard at the top-left of the text, without a hunk.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { } diff || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository)
            || !ReferenceEquals(repository.Diff, diff) || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        MenuPosition position;
        MenuOpenedBy openedBy;
        int? hunk = null;
        if (e.TryGetPosition(topLevel, out var pointer))
        {
            (position, openedBy) = (new MenuPosition(pointer.X, pointer.Y), MenuOpenedBy.Pointer);
            if (diff.ShowsText && e.TryGetPosition(DiffText, out var inText))
            {
                hunk = HunkAt(inText);
            }
        }
        else
        {
            var corner = DiffTextHost.TranslatePoint(default, topLevel) ?? default;
            (position, openedBy) = (new MenuPosition(corner.X, corner.Y), MenuOpenedBy.Keyboard);
        }

        e.Handled = repository.OpenDiffMenu(layer, diff, hunk, position, openedBy);
    }

    /// <summary>The hunk whose rows (its header included) are drawn at <paramref name="point"/> of the text, or null outside every hunk.</summary>
    private int? HunkAt(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X >= DiffText.Bounds.Width || point.Y >= DiffText.Bounds.Height)
        {
            return null;
        }

        var rows = DiffText.Rows;
        for (var row = 0; row < rows.Count; row++)
        {
            if (DiffText.RowBounds(row) is { } bounds && point.Y >= bounds.Top && point.Y < bounds.Bottom)
            {
                return rows[row].Hunk >= 0 ? rows[row].Hunk : null;
            }
        }

        return null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // The selection belongs to the text control, which every tab's diff shares: a diff that
        // leaves it, or comes to it, has no lines selected.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.SelectionResetRequested -= OnSelectionResetRequested;
            _viewModel.SelectedChanges = new HashSet<DiffLineRef>();
        }

        _viewModel = null;
        DiffText.ClearSelection();
        _viewModel = DataContext as DiffViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SelectedChanges = new HashSet<DiffLineRef>();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.SelectionResetRequested += OnSelectionResetRequested;

            // A diff that opens takes the keyboard focus, so that Ctrl+End and the like work at once.
            if (TakesFocusOnOpen)
            {
                Dispatcher.UIThread.Post(() => DiffText.FocusBody(), DispatcherPriority.Loaded);
            }
        }

        ShowImages();
        base.OnDataContextChanged(e);
    }

    private void OnSelectionResetRequested(object? sender, EventArgs e) => DiffText.ClearSelection();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiffViewModel.Body) && _viewModel?.ShowsText == true && TakesFocusOnOpen)
        {
            Dispatcher.UIThread.Post(() => DiffText.FocusBody(), DispatcherPriority.Loaded);
        }

        if (e.PropertyName is nameof(DiffViewModel.BeforeImage) or nameof(DiffViewModel.AfterImage) or nameof(DiffViewModel.Body))
        {
            ShowImages();
        }
    }

    /// <summary>The two images and their captions: "<width> × <height> pixels, <N> bytes", or "No image".</summary>
    private void ShowImages()
    {
        var viewModel = _viewModel;
        Show(BeforeImage, BeforeImageFrame, BeforeCaption, viewModel?.BeforeImage, viewModel?.BeforeBytes);
        Show(AfterImage, AfterImageFrame, AfterCaption, viewModel?.AfterImage, viewModel?.AfterBytes);

        static void Show(Image image, Border frame, TextBlock caption, byte[]? bytes, long? size)
        {
            (image.Source as IDisposable)?.Dispose();
            image.Source = null;
            if (bytes is null)
            {
                frame.IsVisible = false;
                caption.Text = "No image";
                return;
            }

            try
            {
                var bitmap = new Bitmap(new MemoryStream(bytes));
                image.Source = bitmap;
                frame.IsVisible = true;
                caption.Text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height} pixels, {DiffViewModel.Bytes(size ?? bytes.LongLength)}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
            {
                // Not an image Avalonia can read, though its name says so: its size still shows.
                frame.IsVisible = false;
                caption.Text = DiffViewModel.Bytes(size ?? bytes.LongLength);
            }
        }
    }
}
