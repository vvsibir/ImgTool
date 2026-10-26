using System.Windows;
using System.Windows.Controls;
using ImgTool.App.ViewModels;

namespace ImgTool.App.Views;

public partial class VideoView : UserControl
{
    public VideoView()
    {
        InitializeComponent();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not VideoViewModel viewModel)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            viewModel.AddDroppedFiles(files);
        }
    }
}
