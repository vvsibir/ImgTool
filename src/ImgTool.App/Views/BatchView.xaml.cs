using System.Windows;
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
}
