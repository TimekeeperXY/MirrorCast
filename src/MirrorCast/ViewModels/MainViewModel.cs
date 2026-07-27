using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using MirrorCast.Models;
using MirrorCast.Services;
using Application = System.Windows.Application;

namespace MirrorCast.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ConfigService _configService = new();
    private readonly ThumbnailController _controller = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly AppConfig _config;

    public IntPtr SelfHwnd { get; set; }

    public ObservableCollection<WindowInfo> Windows { get; } = new();
    public ObservableCollection<MonitorInfo> Monitors { get; } = new();
    public ObservableCollection<ScaleMode> ScaleModes { get; } = new(Enum.GetValues<ScaleMode>());

    public ICollectionView WindowsView { get; }
    public ICollectionView SwitchWindowsView { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                WindowsView.Refresh();
                SwitchWindowsView.Refresh();
            }
        }
    }

    private WindowInfo? _selectedWindow;
    public WindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set { if (SetField(ref _selectedWindow, value)) StartCommand.RaiseCanExecuteChanged(); }
    }

    private MonitorInfo? _selectedMonitor;
    public MonitorInfo? SelectedMonitor
    {
        get => _selectedMonitor;
        set { if (SetField(ref _selectedMonitor, value)) StartCommand.RaiseCanExecuteChanged(); }
    }

    private ScaleMode _scaleMode = ScaleMode.Fit;
    public ScaleMode ScaleMode
    {
        get => _scaleMode;
        set => SetField(ref _scaleMode, value);
    }

    private bool _clientAreaOnly = true;
    public bool ClientAreaOnly
    {
        get => _clientAreaOnly;
        set => SetField(ref _clientAreaOnly, value);
    }

    private bool _hideCursor = true;
    public bool HideCursor
    {
        get => _hideCursor;
        set => SetField(ref _hideCursor, value);
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetField(ref _startWithWindows, value))
                StartupService.SetEnabled(value);
        }
    }

    private bool _isMirroring;
    public bool IsMirroring
    {
        get => _isMirroring;
        set
        {
            if (SetField(ref _isMirroring, value))
            {
                OnPropertyChanged(nameof(ShowMirroringStatus));
                OnPropertyChanged(nameof(ShowSwitchPanel));
            }
        }
    }

    private bool _isSwitchingWindow;
    public bool IsSwitchingWindow
    {
        get => _isSwitchingWindow;
        set
        {
            if (SetField(ref _isSwitchingWindow, value))
            {
                OnPropertyChanged(nameof(ShowMirroringStatus));
                OnPropertyChanged(nameof(ShowSwitchPanel));
            }
        }
    }

    public bool ShowMirroringStatus => IsMirroring && !IsSwitchingWindow;
    public bool ShowSwitchPanel => IsMirroring && IsSwitchingWindow;

    private bool _canStart = true;
    public bool CanStart
    {
        get => _canStart;
        set => SetField(ref _canStart, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand OpenSwitchWindowCommand { get; }
    public RelayCommand CancelSwitchWindowCommand { get; }

    public event Action? ShowMainWindowRequested;
    public event Action<string>? Notify;

    public MainViewModel()
    {
        WindowsView = CollectionViewSource.GetDefaultView(Windows);
        WindowsView.Filter = FilterWindow;

        // Independent view so switch-mode exclusion filtering never touches the idle
        // panel's ListBox selection (they'd otherwise fight over a shared CollectionView).
        SwitchWindowsView = new ListCollectionView(Windows) { Filter = FilterSwitchWindow };

        RefreshCommand = new RelayCommand(RefreshWindows);
        StartCommand = new RelayCommand(StartMirroring, () => !IsMirroring && SelectedWindow != null && SelectedMonitor != null && Monitors.Count > 1);
        StopCommand = new RelayCommand(StopMirroring, () => IsMirroring);
        OpenSwitchWindowCommand = new RelayCommand(OpenSwitchWindow, () => IsMirroring);
        CancelSwitchWindowCommand = new RelayCommand(() => IsSwitchingWindow = false);

        _controller.SourceClosed += OnSourceClosed;
        _controller.TargetMonitorLost += OnTargetMonitorLost;
        _controller.StoppedByUser += () => Application.Current.Dispatcher.Invoke(StopMirroring);

        _config = _configService.Load();
        ScaleMode = _config.ScaleMode;
        ClientAreaOnly = _config.ClientAreaOnly;
        HideCursor = _config.HideCursor;
        _startWithWindows = StartupService.IsEnabled();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => { if (!IsMirroring) RefreshWindows(); };
        _refreshTimer.Start();
    }

    public void Initialize(IntPtr selfHwnd)
    {
        SelfHwnd = selfHwnd;
        RefreshMonitors();
        RefreshWindows();
        RestoreLastSelection();
    }

    private bool FilterWindow(object obj)
    {
        if (obj is not WindowInfo w) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return w.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || w.ProcessName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterSwitchWindow(object obj)
    {
        if (obj is not WindowInfo w) return false;
        if (SelectedWindow != null && w.Hwnd == SelectedWindow.Hwnd) return false;
        return FilterWindow(obj);
    }

    public void RefreshWindows()
    {
        var current = SelectedWindow;
        var list = WindowEnumerator.EnumerateMirrorableWindows(SelfHwnd);

        Windows.Clear();
        foreach (var w in list) Windows.Add(w);

        SelectedWindow = current != null
            ? Windows.FirstOrDefault(w => w.Hwnd == current.Hwnd) ?? Windows.FirstOrDefault()
            : Windows.FirstOrDefault();

        WindowsView.Refresh();
        SwitchWindowsView.Refresh();
    }

    public void RefreshMonitors()
    {
        var list = MonitorEnumerator.EnumerateMonitors();
        Monitors.Clear();
        foreach (var m in list) Monitors.Add(m);

        SelectedMonitor = Monitors.FirstOrDefault(m => !m.IsPrimary) ?? Monitors.FirstOrDefault();
        CanStart = Monitors.Count > 1;
        StartCommand.RaiseCanExecuteChanged();
    }

    private void RestoreLastSelection()
    {
        if (_config.LastProcessName != null)
        {
            var match = Windows.FirstOrDefault(w =>
                w.ProcessName.Equals(_config.LastProcessName, StringComparison.OrdinalIgnoreCase) &&
                w.Title == _config.LastWindowTitle);
            if (match != null) SelectedWindow = match;
        }

        if (_config.LastMonitorDeviceName != null)
        {
            var match = Monitors.FirstOrDefault(m => m.DeviceName == _config.LastMonitorDeviceName);
            if (match != null) SelectedMonitor = match;
        }
    }

    private void StartMirroring()
    {
        if (SelectedWindow == null || SelectedMonitor == null) return;

        try
        {
            var options = new MirrorOptions
            {
                ScaleMode = ScaleMode,
                ClientAreaOnly = ClientAreaOnly,
                HideCursor = HideCursor
            };

            _controller.Start(SelectedWindow.Hwnd, SelectedMonitor, options);
            IsMirroring = true;

            _refreshTimer.Stop();
            AddRecentWindow(SelectedWindow);
            SaveConfig();

            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Notify?.Invoke($"镜像启动失败：{ex.Message}");
        }
    }

    public void StopMirroring()
    {
        _controller.Stop();
        IsMirroring = false;
        IsSwitchingWindow = false;
        _refreshTimer.Start();
        RefreshMonitors();
        RefreshWindows();

        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }

    public void ToggleMirroring()
    {
        if (IsMirroring) StopMirroring();
        else if (StartCommand.CanExecute(null)) StartMirroring();
    }

    private void OpenSwitchWindow()
    {
        IsSwitchingWindow = true;
        RefreshWindows();
    }

    public void SwitchWindow(WindowInfo? newWindow)
    {
        if (newWindow == null || newWindow.Hwnd == SelectedWindow?.Hwnd)
        {
            IsSwitchingWindow = false;
            return;
        }

        try
        {
            _controller.SwitchSource(newWindow.Hwnd);
            SelectedWindow = newWindow;
            AddRecentWindow(newWindow);
            SaveConfig();
        }
        catch (Exception ex)
        {
            Notify?.Invoke($"切换镜像窗口失败：{ex.Message}");
        }
        finally
        {
            IsSwitchingWindow = false;
        }
    }

    private void OnSourceClosed()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StopMirroring();
            Notify?.Invoke("源窗口已关闭，镜像已停止");
            ShowMainWindowRequested?.Invoke();
        });
    }

    private void OnTargetMonitorLost()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StopMirroring();
            Notify?.Invoke("目标显示器已断开，镜像已停止");
            ShowMainWindowRequested?.Invoke();
        });
    }

    private void AddRecentWindow(WindowInfo window)
    {
        _config.RecentWindows.RemoveAll(r => r.ProcessName == window.ProcessName && r.Title == window.Title);
        _config.RecentWindows.Insert(0, new RecentWindowEntry { ProcessName = window.ProcessName, Title = window.Title });
        if (_config.RecentWindows.Count > 5)
            _config.RecentWindows.RemoveRange(5, _config.RecentWindows.Count - 5);
    }

    public void SaveConfig()
    {
        _config.LastProcessName = SelectedWindow?.ProcessName;
        _config.LastWindowTitle = SelectedWindow?.Title;
        _config.LastMonitorDeviceName = SelectedMonitor?.DeviceName;
        _config.ScaleMode = ScaleMode;
        _config.ClientAreaOnly = ClientAreaOnly;
        _config.HideCursor = HideCursor;
        _config.StartWithWindows = StartWithWindows;
        _configService.Save(_config);
    }
}
