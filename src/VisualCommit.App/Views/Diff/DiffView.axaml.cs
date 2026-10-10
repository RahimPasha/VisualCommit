using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VisualCommit.App.ViewModels.Diff;

namespace VisualCommit.App.Views.Diff;

/// <summary>
/// The diff view (C5): see <see cref="DiffViewModel"/>. The code-behind connects what bindings
/// cannot: the images and their captions, and the diff text control's selection and hunk buttons.
/// </summary>
public partial class DiffView : UserControl
{
    private DiffViewModel? _viewModel;

    public DiffView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DiffViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ShowImages();
        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
