using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ImgTool.App.Settings;
using ImgTool.App.ViewModels;

namespace ImgTool.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public MainWindow()
        : this(new SettingsStore())
    {
    }

    public MainWindow(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = settingsStore.Load();

        InitializeComponent();

        var viewModel = new MainViewModel();
        SettingsMapper.Apply(viewModel, _settings);
        DataContext = viewModel;

        ApplyWindowSettings(_settings.Window);
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Перетаскивание могло быть прервано системой.
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;
        WindowBorder.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        MaximizeButton.Content = maximized ? "❐" : "□";
        MaximizeButton.ToolTip = maximized ? "Восстановить" : "Развернуть";
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        try
        {
            _settings.Window = CaptureWindowSettings();
            SettingsMapper.Capture(viewModel, _settings);
            _settingsStore.TrySave(_settings);
        }
        finally
        {
            viewModel.Shutdown();
        }
    }

    private void ApplyWindowSettings(WindowSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        if (settings.Left is double left &&
            settings.Top is double top &&
            settings.Width is double width &&
            settings.Height is double height &&
            double.IsFinite(left) &&
            double.IsFinite(top) &&
            double.IsFinite(width) &&
            double.IsFinite(height) &&
            width > 0 &&
            height > 0)
        {
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var savedBounds = new Rect(left, top, Math.Max(MinWidth, width), Math.Max(MinHeight, height));

            if (IsVisibleOnScreen(savedBounds, virtualScreen))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = savedBounds.Left;
                Top = savedBounds.Top;
                Width = savedBounds.Width;
                Height = savedBounds.Height;
            }
        }

        if (settings.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private WindowSettings CaptureWindowSettings()
    {
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (bounds.IsEmpty ||
            !double.IsFinite(bounds.Left) ||
            !double.IsFinite(bounds.Top) ||
            !double.IsFinite(bounds.Width) ||
            !double.IsFinite(bounds.Height) ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            bounds = new Rect(Left, Top, Width, Height);
        }

        return new WindowSettings
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized
        };
    }

    private static bool IsVisibleOnScreen(Rect window, Rect virtualScreen)
    {
        const double minimumVisibleWidth = 120;
        const double minimumVisibleHeight = 60;

        double visibleWidth = Math.Min(window.Right, virtualScreen.Right) -
                              Math.Max(window.Left, virtualScreen.Left);
        double visibleHeight = Math.Min(window.Bottom, virtualScreen.Bottom) -
                               Math.Max(window.Top, virtualScreen.Top);

        return visibleWidth >= minimumVisibleWidth && visibleHeight >= minimumVisibleHeight;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
