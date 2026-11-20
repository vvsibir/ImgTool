using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImgTool.App.ViewModels;

namespace ImgTool.App.Views;

public partial class BatchView : System.Windows.Controls.UserControl
{
    public BatchView()
    {
        InitializeComponent();
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not BatchViewModel viewModel)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            viewModel.AddPaths(paths);
            e.Handled = true;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BatchViewModel viewModel || !viewModel.IsPickingChromaKeyColor)
        {
            return;
        }

        if (viewModel.PreviewSource is not BitmapSource source)
        {
            return;
        }

        if (sender is not System.Windows.Controls.Image image || image.ActualWidth <= 0 || image.ActualHeight <= 0)
        {
            return;
        }

        Point pos = e.GetPosition(image);
        double scale = Math.Min(image.ActualWidth / source.PixelWidth, image.ActualHeight / source.PixelHeight);
        if (scale <= 0)
        {
            return;
        }

        double drawWidth = source.PixelWidth * scale;
        double drawHeight = source.PixelHeight * scale;
        double offsetX = (image.ActualWidth - drawWidth) / 2.0;
        double offsetY = (image.ActualHeight - drawHeight) / 2.0;

        double x = pos.X - offsetX;
        double y = pos.Y - offsetY;
        if (x < 0 || y < 0 || x >= drawWidth || y >= drawHeight)
        {
            return;
        }

        int pixelX = Math.Min(source.PixelWidth - 1, Math.Max(0, (int)(x / scale)));
        int pixelY = Math.Min(source.PixelHeight - 1, Math.Max(0, (int)(y / scale)));

        var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = formatted.PixelWidth * 4;
        byte[] pixels = new byte[stride];
        formatted.CopyPixels(new Int32Rect(pixelX, pixelY, 1, 1), pixels, stride, 0);

        byte b = pixels[0];
        byte g = pixels[1];
        byte r = pixels[2];
        Color color = Color.FromRgb(r, g, b);
        viewModel.ApplyPickedChromaKeyColor(color);
        e.Handled = true;
    }
}
