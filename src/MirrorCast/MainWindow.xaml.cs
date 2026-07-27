using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MirrorCast.Models;
using MirrorCast.Services;
using MirrorCast.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace MirrorCast;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private readonly HotkeyService _hotkeyService = new();
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        Icon = IconExtractor.GetAppIconBitmapSource();

        ViewModel.ShowMainWindowRequested += () => Dispatcher.Invoke(ShowAndActivate);
        ViewModel.Notify += msg => Dispatcher.Invoke(() =>
            MessageBox.Show(msg, "MirrorCast", MessageBoxButton.OK, MessageBoxImage.Information));

        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        ViewModel.Initialize(hwnd);

        _hotkeyService.Initialize(this);
        _hotkeyService.Register("Ctrl+Alt+M", () => Dispatcher.Invoke(ViewModel.ToggleMirroring));
        _hotkeyService.Register("Ctrl+Alt+Shift+M", () => Dispatcher.Invoke(() =>
        {
            ViewModel.StopMirroring();
            ShowAndActivate();
        }));
    }

    private void SwitchListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is WindowInfo window)
        {
            ViewModel.SwitchWindow(window);
        }
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void AllowClose()
    {
        _allowClose = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            ViewModel.StopMirroring();
            ViewModel.SaveConfig();
            _hotkeyService.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
