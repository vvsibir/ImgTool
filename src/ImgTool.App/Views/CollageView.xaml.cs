using System.Windows;
using ImgTool.App.ViewModels;

namespace ImgTool.App.Views;

public partial class CollageView : System.Windows.Controls.UserControl
{
    public CollageView()
    {
        InitializeComponent();
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not CollageViewModel viewModel)
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
