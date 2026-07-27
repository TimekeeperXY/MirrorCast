using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using MirrorCast.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MirrorCast;

public partial class App : Application
{
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private System.Threading.Mutex? _mutex;
    private ThemeService? _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new System.Threading.Mutex(true, "MirrorCast.SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("MirrorCast 已在运行中。", "MirrorCast", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/ControlStyles.xaml", UriKind.Relative)
        });

        _themeService = new ThemeService();
        _themeService.ThemeChanged += OnSystemThemeChanged;
        ApplyColorTheme(_themeService.IsDarkTheme);

        _mainWindow = new MainWindow();
        SetupTrayIcon();
        _mainWindow.Show();

        BackdropService.Apply(_mainWindow, _themeService.IsDarkTheme);
    }

    private void OnSystemThemeChanged(bool isDarkTheme)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyColorTheme(isDarkTheme);
            if (_mainWindow != null)
                BackdropService.Apply(_mainWindow, isDarkTheme);
        });
    }

    private void ApplyColorTheme(bool isDarkTheme)
    {
        var uri = new Uri(isDarkTheme ? "Themes/Colors.Dark.xaml" : "Themes/Colors.Light.xaml", UriKind.Relative);
        var existing = Resources.MergedDictionaries.FirstOrDefault(d => d.Source is { } s && s.OriginalString.Contains("Colors."));
        if (existing != null) Resources.MergedDictionaries.Remove(existing);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = IconExtractor.GetAppIcon() ?? SystemIcons.Application,
            Visible = true,
            Text = "MirrorCast"
        };

        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示主界面");
        showItem.Click += (_, _) => _mainWindow?.ShowAndActivate();

        var toggleItem = new ToolStripMenuItem("开始镜像");
        toggleItem.Click += (_, _) => _mainWindow?.ViewModel.ToggleMirroring();

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon!.Visible = false;
            _mainWindow?.AllowClose();
            Shutdown();
        };

        menu.Opening += (_, _) =>
        {
            toggleItem.Text = _mainWindow?.ViewModel.IsMirroring == true ? "停止镜像" : "开始镜像";
        };

        menu.Items.Add(showItem);
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => _mainWindow?.ShowAndActivate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _themeService?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
