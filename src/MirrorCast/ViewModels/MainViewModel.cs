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
    private enum PresentationFeature
    {
        ScreenZoom,
        Magnifier,
        Spotlight,
        Annotation
    }

    private readonly ConfigService _configService = new();
    private readonly ThumbnailController _controller = new();
    private readonly AndroidMirrorService _androidMirror = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly AppConfig _config;

    public IntPtr SelfHwnd { get; set; }

    public ObservableCollection<WindowInfo> Windows { get; } = new();
    public ObservableCollection<MonitorInfo> Monitors { get; } = new();
    public ObservableCollection<ScaleMode> ScaleModes { get; } = new(Enum.GetValues<ScaleMode>());
    public ObservableCollection<AndroidDeviceInfo> AndroidDevices { get; } = new();
    public ObservableCollection<int> AndroidFrameRates { get; } = new([30, 60, 90, 120, 165]);

    public ICollectionView WindowsView { get; }
    public ICollectionView SwitchWindowsView { get; }

    private AndroidDeviceInfo? _selectedAndroidDevice;
    public AndroidDeviceInfo? SelectedAndroidDevice
    {
        get => _selectedAndroidDevice;
        set { if (SetField(ref _selectedAndroidDevice, value)) StartAndroidCommand.RaiseCanExecuteChanged(); }
    }

    private string _androidAddress = string.Empty;
    public string AndroidAddress
    {
        get => _androidAddress;
        set { if (SetField(ref _androidAddress, value)) StartAndroidCommand.RaiseCanExecuteChanged(); }
    }

    private int _androidPort = 5555;
    public int AndroidPort { get => _androidPort; set => SetField(ref _androidPort, Math.Clamp(value, 1, 65535)); }

    private int _androidMaxFps = 60;
    public int AndroidMaxFps { get => _androidMaxFps; set => SetField(ref _androidMaxFps, value); }

    private bool _androidControl = true;
    public bool AndroidControl { get => _androidControl; set => SetField(ref _androidControl, value); }

    private bool _androidAudio = true;
    public bool AndroidAudio { get => _androidAudio; set => SetField(ref _androidAudio, value); }

    private bool _androidTurnScreenOff;
    public bool AndroidTurnScreenOff { get => _androidTurnScreenOff; set => SetField(ref _androidTurnScreenOff, value); }

    private bool _isAndroidBusy;
    public bool IsAndroidBusy
    {
        get => _isAndroidBusy;
        private set
        {
            if (!SetField(ref _isAndroidBusy, value)) return;
            OnPropertyChanged(nameof(AndroidActionText));
            RefreshAndroidCommand.RaiseCanExecuteChanged();
            StartAndroidCommand.RaiseCanExecuteChanged();
        }
    }

    private string _androidStatus = string.Empty;
    public string AndroidStatus { get => _androidStatus; private set => SetField(ref _androidStatus, value); }
    public bool IsAndroidAvailable => _androidMirror.IsAvailable;
    public string AndroidActionText => IsAndroidBusy ? "正在连接…" : "投到副屏";

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

    private bool _showSyntheticCursor = true;
    public bool ShowSyntheticCursor
    {
        get => _showSyntheticCursor;
        set => SetField(ref _showSyntheticCursor, value);
    }

    private double _presentationZoomFactor = 2.0;
    public double PresentationZoomFactor
    {
        get => _presentationZoomFactor;
        set => SetField(ref _presentationZoomFactor, Math.Round(Math.Clamp(value, 1.25, 5.0), 2));
    }

    private double _pointerEffectSize = 240;
    public double PointerEffectSize
    {
        get => _pointerEffectSize;
        set => SetField(ref _pointerEffectSize, Math.Round(Math.Clamp(value, 120, 480)));
    }

    private bool _presentationKeyModeEnabled = true;
    public bool PresentationKeyModeEnabled
    {
        get => _presentationKeyModeEnabled;
        set
        {
            if (!SetField(ref _presentationKeyModeEnabled, value)) return;
            PresentationKeyModeChanged?.Invoke();
            SaveConfig();
            OnPropertyChanged(nameof(ScreenZoomButtonText));
            OnPropertyChanged(nameof(MagnifierButtonText));
            OnPropertyChanged(nameof(SpotlightButtonText));
            OnPropertyChanged(nameof(AnnotationButtonText));
        }
    }

    private string _toggleHotkey = "Ctrl+Alt+M";
    public string ToggleHotkey
    {
        get => _toggleHotkey;
        set
        {
            if (SetField(ref _toggleHotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplay));
                OnPropertyChanged(nameof(StartButtonText));
                OnPropertyChanged(nameof(StopButtonText));
            }
        }
    }

    private string _screenZoomHotkey = "Ctrl+Alt+Shift+Z";
    public string ScreenZoomHotkey
    {
        get => _screenZoomHotkey;
        set
        {
            if (SetField(ref _screenZoomHotkey, value))
            {
                OnPropertyChanged(nameof(ScreenZoomHotkeyDisplay));
                OnPropertyChanged(nameof(ScreenZoomButtonText));
            }
        }
    }

    private string _magnifierHotkey = "Ctrl+Alt+Shift+L";
    public string MagnifierHotkey
    {
        get => _magnifierHotkey;
        set
        {
            if (SetField(ref _magnifierHotkey, value))
            {
                OnPropertyChanged(nameof(MagnifierHotkeyDisplay));
                OnPropertyChanged(nameof(MagnifierButtonText));
            }
        }
    }

    private string _spotlightHotkey = "Ctrl+Alt+Shift+P";
    public string SpotlightHotkey
    {
        get => _spotlightHotkey;
        set
        {
            if (SetField(ref _spotlightHotkey, value))
            {
                OnPropertyChanged(nameof(SpotlightHotkeyDisplay));
                OnPropertyChanged(nameof(SpotlightButtonText));
            }
        }
    }

    private string _annotationHotkey = "Ctrl+Alt+Shift+A";
    public string AnnotationHotkey
    {
        get => _annotationHotkey;
        set
        {
            if (SetField(ref _annotationHotkey, value))
            {
                OnPropertyChanged(nameof(AnnotationHotkeyDisplay));
                OnPropertyChanged(nameof(AnnotationButtonText));
            }
        }
    }

    private HotkeyAction? _capturingHotkeyAction;
    public HotkeyAction? CapturingHotkeyAction
    {
        get => _capturingHotkeyAction;
        private set
        {
            if (SetField(ref _capturingHotkeyAction, value))
            {
                OnPropertyChanged(nameof(IsCapturingHotkey));
                OnPropertyChanged(nameof(HotkeyDisplay));
                OnPropertyChanged(nameof(ScreenZoomHotkeyDisplay));
                OnPropertyChanged(nameof(MagnifierHotkeyDisplay));
                OnPropertyChanged(nameof(SpotlightHotkeyDisplay));
                OnPropertyChanged(nameof(AnnotationHotkeyDisplay));
            }
        }
    }

    public bool IsCapturingHotkey => CapturingHotkeyAction != null;
    public string HotkeyDisplay => CapturingHotkeyAction == HotkeyAction.Mirror ? "请按下新的快捷键…" : ToggleHotkey;
    public string ScreenZoomHotkeyDisplay => CapturingHotkeyAction == HotkeyAction.ScreenZoom ? "请按快捷键…" : ScreenZoomHotkey;
    public string MagnifierHotkeyDisplay => CapturingHotkeyAction == HotkeyAction.Magnifier ? "请按快捷键…" : MagnifierHotkey;
    public string SpotlightHotkeyDisplay => CapturingHotkeyAction == HotkeyAction.Spotlight ? "请按快捷键…" : SpotlightHotkey;
    public string AnnotationHotkeyDisplay => CapturingHotkeyAction == HotkeyAction.Annotation ? "请按快捷键…" : AnnotationHotkey;
    public string StartButtonText => $"开始镜像  ({ToggleHotkey})";
    public string StopButtonText => $"停止镜像  ({ToggleHotkey})";

    /// <summary>Raised when the shortcut changed and needs re-registering with Windows.</summary>
    public event Func<HotkeyAction, string, bool>? HotkeyChangeRequested;

    private bool _isOnboardingVisible;
    public bool IsOnboardingVisible
    {
        get => _isOnboardingVisible;
        set => SetField(ref _isOnboardingVisible, value);
    }

    /// <summary>True only until the walkthrough has been completed or skipped once.</summary>
    public bool ShouldShowOnboarding => !_config.HasSeenOnboarding;

    public void CompleteOnboarding()
    {
        IsOnboardingVisible = false;
        _config.HasSeenOnboarding = true;
        SaveConfig();
    }

    public void BeginCaptureHotkey(HotkeyAction action) => CapturingHotkeyAction = action;
    public void CancelCaptureHotkey() => CapturingHotkeyAction = null;

    /// <summary>Applies a newly captured combo, keeping the old one if Windows refuses it.</summary>
    public void ApplyCapturedHotkey(string hotkey)
    {
        if (CapturingHotkeyAction is not { } action) return;
        CapturingHotkeyAction = null;
        if (hotkey == GetHotkey(action)) return;

        if (HotkeyChangeRequested?.Invoke(action, hotkey) == true)
        {
            SetHotkey(action, hotkey);
            SaveConfig();
        }
        else
        {
            Notify?.Invoke($"快捷键 {hotkey} 无法注册，可能已被其他程序占用，已保留原设置。");
        }
    }

    public string GetHotkey(HotkeyAction action) => action switch
    {
        HotkeyAction.Mirror => ToggleHotkey,
        HotkeyAction.ScreenZoom => ScreenZoomHotkey,
        HotkeyAction.Magnifier => MagnifierHotkey,
        HotkeyAction.Spotlight => SpotlightHotkey,
        HotkeyAction.Annotation => AnnotationHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private void SetHotkey(HotkeyAction action, string hotkey)
    {
        switch (action)
        {
            case HotkeyAction.Mirror: ToggleHotkey = hotkey; break;
            case HotkeyAction.ScreenZoom: ScreenZoomHotkey = hotkey; break;
            case HotkeyAction.Magnifier: MagnifierHotkey = hotkey; break;
            case HotkeyAction.Spotlight: SpotlightHotkey = hotkey; break;
            case HotkeyAction.Annotation: AnnotationHotkey = hotkey; break;
        }
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

    private bool _isScreenZoomActive;
    public bool IsScreenZoomActive
    {
        get => _isScreenZoomActive;
        private set
        {
            if (SetField(ref _isScreenZoomActive, value))
                OnPropertyChanged(nameof(ScreenZoomButtonText));
        }
    }

    private bool _isMagnifierActive;
    public bool IsMagnifierActive
    {
        get => _isMagnifierActive;
        private set
        {
            if (SetField(ref _isMagnifierActive, value))
                OnPropertyChanged(nameof(MagnifierButtonText));
        }
    }

    private bool _isSpotlightActive;
    public bool IsSpotlightActive
    {
        get => _isSpotlightActive;
        private set
        {
            if (SetField(ref _isSpotlightActive, value))
                OnPropertyChanged(nameof(SpotlightButtonText));
        }
    }

    private bool _isAnnotationActive;
    public bool IsAnnotationActive
    {
        get => _isAnnotationActive;
        private set
        {
            if (SetField(ref _isAnnotationActive, value))
                OnPropertyChanged(nameof(AnnotationButtonText));
        }
    }

    private string KeyHint(string functionKey, string configuredHotkey)
        => PresentationKeyModeEnabled ? $"{functionKey} / {configuredHotkey}" : configuredHotkey;

    public string ScreenZoomButtonText => $"{(IsScreenZoomActive ? "关闭" : "开启")}屏幕放大  ({KeyHint("F1", ScreenZoomHotkey)})";
    public string MagnifierButtonText => $"{(IsMagnifierActive ? "关闭" : "开启")}指针放大镜  ({KeyHint("F2", MagnifierHotkey)})";
    public string SpotlightButtonText => $"{(IsSpotlightActive ? "关闭" : "开启")}指针聚光灯  ({KeyHint("F3", SpotlightHotkey)})";
    public string AnnotationButtonText => $"{(IsAnnotationActive ? "退出" : "进入")}屏幕标注  ({KeyHint("F4", AnnotationHotkey)})";

    private readonly List<PresentationFeature> _activeFeatureOrder = [];
    private DateTime _lastPresentationEscape = DateTime.MinValue;

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
    public RelayCommand ToggleScreenZoomCommand { get; }
    public RelayCommand ToggleMagnifierCommand { get; }
    public RelayCommand ToggleSpotlightCommand { get; }
    public RelayCommand ToggleAnnotationCommand { get; }
    public RelayCommand RefreshAndroidCommand { get; }
    public RelayCommand StartAndroidCommand { get; }

    public event Action? ShowMainWindowRequested;
    public event Action<string>? Notify;
    public event Action<bool>? MirroringStateChanged;
    public event Action? PresentationKeyModeChanged;

    public void NotifyPresentationHotkeysUnavailable(IEnumerable<string> keys)
        => Notify?.Invoke($"演示快捷键 {string.Join("、", keys)} 已被其他程序占用，请使用原组合快捷键。");

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
        ToggleScreenZoomCommand = new RelayCommand(ToggleScreenZoom, () => IsMirroring);
        ToggleMagnifierCommand = new RelayCommand(ToggleMagnifier, () => IsMirroring);
        ToggleSpotlightCommand = new RelayCommand(ToggleSpotlight, () => IsMirroring);
        ToggleAnnotationCommand = new RelayCommand(ToggleAnnotations, () => IsMirroring);
        RefreshAndroidCommand = new RelayCommand(async () => await RefreshAndroidDevicesAsync(), () => !IsAndroidBusy && !IsMirroring);
        StartAndroidCommand = new RelayCommand(async () => await StartAndroidMirroringAsync(),
            () => !IsAndroidBusy && !IsMirroring && SelectedMonitor != null && Monitors.Count > 1
                && (SelectedAndroidDevice != null || !string.IsNullOrWhiteSpace(AndroidAddress)));

        _controller.SourceClosed += OnSourceClosed;
        _controller.TargetMonitorLost += OnTargetMonitorLost;
        _controller.StoppedByUser += () => Application.Current.Dispatcher.Invoke(ExitPresentationModeOrStop);
        _controller.AnnotationStateChanged += active =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsAnnotationActive = active;
                TrackFeature(PresentationFeature.Annotation, active);
            });

        _config = _configService.Load();
        ScaleMode = _config.ScaleMode;
        ClientAreaOnly = _config.ClientAreaOnly;
        HideCursor = _config.HideCursor;
        ShowSyntheticCursor = _config.ShowSyntheticCursor;
        PresentationZoomFactor = _config.PresentationZoomFactor;
        PointerEffectSize = _config.PointerEffectSize;
        _presentationKeyModeEnabled = _config.PresentationKeyModeEnabled;
        if (!string.IsNullOrWhiteSpace(_config.ToggleHotkey))
            ToggleHotkey = _config.ToggleHotkey;
        if (!string.IsNullOrWhiteSpace(_config.ScreenZoomHotkey))
            ScreenZoomHotkey = _config.ScreenZoomHotkey;
        if (!string.IsNullOrWhiteSpace(_config.MagnifierHotkey))
            MagnifierHotkey = _config.MagnifierHotkey;
        if (!string.IsNullOrWhiteSpace(_config.SpotlightHotkey))
            SpotlightHotkey = _config.SpotlightHotkey;
        if (!string.IsNullOrWhiteSpace(_config.AnnotationHotkey))
            AnnotationHotkey = _config.AnnotationHotkey;
        _startWithWindows = StartupService.IsEnabled();
        AndroidAddress = _config.AndroidAddress;
        AndroidPort = _config.AndroidPort;
        AndroidMaxFps = _config.AndroidMaxFps;
        AndroidControl = _config.AndroidControl;
        AndroidAudio = _config.AndroidAudio;
        AndroidTurnScreenOff = _config.AndroidTurnScreenOff;

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
        StartAndroidCommand.RaiseCanExecuteChanged();
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
                HideCursor = HideCursor,
                ShowSyntheticCursor = ShowSyntheticCursor,
                PresentationZoomFactor = PresentationZoomFactor,
                PointerEffectSize = (int)PointerEffectSize
            };

            _controller.Start(SelectedWindow.Hwnd, SelectedMonitor, options);
            IsMirroring = true;
            _activeFeatureOrder.Clear();
            MirroringStateChanged?.Invoke(true);

            _refreshTimer.Stop();
            AddRecentWindow(SelectedWindow);
            SaveConfig();

            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            ToggleScreenZoomCommand.RaiseCanExecuteChanged();
            ToggleMagnifierCommand.RaiseCanExecuteChanged();
            ToggleSpotlightCommand.RaiseCanExecuteChanged();
            ToggleAnnotationCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Notify?.Invoke($"镜像启动失败：{ex.Message}");
        }
    }

    private async Task RefreshAndroidDevicesAsync()
    {
        if (IsMirroring || IsAndroidBusy) return;
        IsAndroidBusy = true;
        AndroidStatus = IsAndroidAvailable ? "正在查找 USB 和无线 ADB 设备…" : "投屏组件不可用，请安装完整版。";
        try
        {
            if (!IsAndroidAvailable) return;
            var selectedSerial = SelectedAndroidDevice?.Serial;
            var devices = await _androidMirror.DiscoverDevicesAsync();
            AndroidDevices.Clear();
            foreach (var device in devices) AndroidDevices.Add(device);
            SelectedAndroidDevice = AndroidDevices.FirstOrDefault(device => device.Serial == selectedSerial)
                ?? AndroidDevices.FirstOrDefault();
            AndroidStatus = devices.Count == 0
                ? "未发现设备。请连接 USB 并授权，或填写无线调试地址。"
                : $"已发现 {devices.Count} 台安卓设备";
        }
        catch (Exception ex)
        {
            AndroidStatus = ex.Message;
        }
        finally
        {
            IsAndroidBusy = false;
        }
    }

    private async Task StartAndroidMirroringAsync()
    {
        if (SelectedMonitor == null || IsAndroidBusy || IsMirroring) return;
        IsAndroidBusy = true;
        AndroidStatus = "正在启动安卓画面…";
        try
        {
            var useManualAddress = !string.IsNullOrWhiteSpace(AndroidAddress);
            var options = new AndroidMirrorOptions
            {
                Serial = useManualAddress ? null : SelectedAndroidDevice?.Serial,
                Address = useManualAddress ? AndroidAddress : null,
                Port = (ushort)AndroidPort,
                MaxFps = AndroidMaxFps,
                Control = AndroidControl,
                Audio = AndroidAudio,
                TurnScreenOff = AndroidTurnScreenOff
            };
            SaveConfig();
            var hwnd = await _androidMirror.StartAsync(options);
            RefreshWindows();
            SelectedWindow = Windows.FirstOrDefault(window => window.Hwnd == hwnd)
                ?? new WindowInfo
                {
                    Hwnd = hwnd,
                    Title = AndroidMirrorService.WindowTitle,
                    ProcessName = "scrcpy.exe",
                    Icon = IconExtractor.GetWindowIcon(hwnd, "scrcpy.exe")
                };
            StartMirroring();
            if (!IsMirroring)
            {
                _androidMirror.Stop();
                return;
            }
            AndroidStatus = "安卓画面已投到副屏，可使用 F1-F4 演示功能。";
        }
        catch (Exception ex)
        {
            _androidMirror.Stop();
            AndroidStatus = ex.Message;
            Notify?.Invoke($"安卓投屏启动失败：{ex.Message}");
        }
        finally
        {
            IsAndroidBusy = false;
        }
    }

    public void StopMirroring()
    {
        bool wasMirroring = IsMirroring;
        _controller.Stop();
        _androidMirror.Stop();
        SyncPresentationState();
        _activeFeatureOrder.Clear();
        IsMirroring = false;
        if (wasMirroring) MirroringStateChanged?.Invoke(false);
        IsSwitchingWindow = false;
        _refreshTimer.Start();
        RefreshMonitors();
        RefreshWindows();

        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ToggleScreenZoomCommand.RaiseCanExecuteChanged();
        ToggleMagnifierCommand.RaiseCanExecuteChanged();
        ToggleSpotlightCommand.RaiseCanExecuteChanged();
        ToggleAnnotationCommand.RaiseCanExecuteChanged();
    }

    public void ToggleMirroring()
    {
        if (IsMirroring) StopMirroring();
        else if (StartCommand.CanExecute(null)) StartMirroring();
    }

    public void ToggleScreenZoom()
    {
        if (!IsMirroring) return;
        bool active = _controller.ToggleScreenZoom();
        SyncPresentationState();
        TrackFeature(PresentationFeature.ScreenZoom, active);
        TrackFeature(PresentationFeature.Magnifier, IsMagnifierActive);
    }

    public void ToggleMagnifier()
    {
        if (!IsMirroring) return;
        bool active = _controller.ToggleMagnifier();
        SyncPresentationState();
        TrackFeature(PresentationFeature.Magnifier, active);
        TrackFeature(PresentationFeature.ScreenZoom, IsScreenZoomActive);
    }

    public void ToggleSpotlight()
    {
        if (!IsMirroring) return;
        bool active = _controller.ToggleSpotlight();
        SyncPresentationState();
        TrackFeature(PresentationFeature.Spotlight, active);
    }

    public void ToggleAnnotations()
    {
        if (!IsMirroring) return;
        bool active = _controller.ToggleAnnotations();
        SyncPresentationState();
        TrackFeature(PresentationFeature.Annotation, active);
    }

    public void ExitPresentationModeOrStop()
    {
        if (!IsMirroring) return;
        var now = DateTime.UtcNow;
        if (now - _lastPresentationEscape < TimeSpan.FromMilliseconds(250)) return;
        _lastPresentationEscape = now;

        ReconcileFeatureOrder();
        if (_activeFeatureOrder.Count == 0)
        {
            StopMirroring();
            return;
        }

        var feature = _activeFeatureOrder[^1];
        switch (feature)
        {
            case PresentationFeature.ScreenZoom: ToggleScreenZoom(); break;
            case PresentationFeature.Magnifier: ToggleMagnifier(); break;
            case PresentationFeature.Spotlight: ToggleSpotlight(); break;
            case PresentationFeature.Annotation: ToggleAnnotations(); break;
        }
    }

    private void TrackFeature(PresentationFeature feature, bool active)
    {
        _activeFeatureOrder.Remove(feature);
        if (active) _activeFeatureOrder.Add(feature);
    }

    private void ReconcileFeatureOrder()
    {
        _activeFeatureOrder.RemoveAll(feature => feature switch
        {
            PresentationFeature.ScreenZoom => !IsScreenZoomActive,
            PresentationFeature.Magnifier => !IsMagnifierActive,
            PresentationFeature.Spotlight => !IsSpotlightActive,
            PresentationFeature.Annotation => !IsAnnotationActive,
            _ => true
        });
    }

    private void SyncPresentationState()
    {
        IsScreenZoomActive = _controller.IsScreenZoomEnabled;
        IsMagnifierActive = _controller.IsMagnifierEnabled;
        IsSpotlightActive = _controller.IsSpotlightEnabled;
        IsAnnotationActive = _controller.IsAnnotationEnabled;
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
        _config.ShowSyntheticCursor = ShowSyntheticCursor;
        _config.ToggleHotkey = ToggleHotkey;
        _config.PresentationZoomFactor = PresentationZoomFactor;
        _config.PointerEffectSize = (int)PointerEffectSize;
        _config.PresentationKeyModeEnabled = PresentationKeyModeEnabled;
        _config.ScreenZoomHotkey = ScreenZoomHotkey;
        _config.MagnifierHotkey = MagnifierHotkey;
        _config.SpotlightHotkey = SpotlightHotkey;
        _config.AnnotationHotkey = AnnotationHotkey;
        _config.AndroidAddress = AndroidAddress;
        _config.AndroidPort = AndroidPort;
        _config.AndroidMaxFps = AndroidMaxFps;
        _config.AndroidControl = AndroidControl;
        _config.AndroidAudio = AndroidAudio;
        _config.AndroidTurnScreenOff = AndroidTurnScreenOff;
        _config.StartWithWindows = StartWithWindows;
        _configService.Save(_config);
    }
}
