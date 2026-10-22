using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ImgTool.App;

public partial class App : Application
{
    private static string LogPath =>
        Path.Combine(Path.GetTempPath(), "imgtool-startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            MessageBox.Show(
                "Не удалось запустить imgTool:\n\n" + ex.Message,
                "imgTool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        MessageBox.Show(
            "Произошла непредвиденная ошибка:\n\n" + e.Exception.Message,
            "imgTool",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void WriteLog(Exception exception)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Логирование не должно мешать запуску.
        }
    }
}
