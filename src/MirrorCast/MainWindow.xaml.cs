using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

        OnboardingOverlay.SizeChanged += (_, _) => UpdateOnboardingSpotlight();
        OnboardingHighlight.LayoutUpdated += (_, _) => UpdateOnboardingSpotlight();

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
        ViewModel.HotkeyChangeRequested += TryApplyHotkey;
        RegisterHotkeys(ViewModel.ToggleHotkey);

        if (ViewModel.ShouldShowOnboarding)
        {
            // Wait for layout so the highlight ring can be placed over real control bounds.
            Dispatcher.BeginInvoke(new Action(StartOnboarding), DispatcherPriority.Loaded);
        }
    }

    private void RegisterHotkeys(string toggleHotkey)
    {
        _hotkeyService.UnregisterAll();
        _hotkeyService.Register(toggleHotkey, () => Dispatcher.Invoke(ViewModel.ToggleMirroring));
        _hotkeyService.Register("Ctrl+Alt+Shift+M", () => Dispatcher.Invoke(() =>
        {
            ViewModel.StopMirroring();
            ShowAndActivate();
        }));
    }

    /// <summary>Swaps in a new shortcut, rolling back to the old one if Windows rejects it.</summary>
    private bool TryApplyHotkey(string hotkey)
    {
        if (!_hotkeyService.IsAvailable(hotkey))
        {
            RegisterHotkeys(ViewModel.ToggleHotkey);
            return false;
        }

        RegisterHotkeys(hotkey);
        return true;
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginCaptureHotkey();
        Keyboard.Focus(HotkeyButton);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!ViewModel.IsCapturingHotkey) return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            ViewModel.CancelCaptureHotkey();
            return;
        }

        var combo = HotkeyService.Format(Keyboard.Modifiers, key);
        if (combo == null) return; // still waiting for a full combo

        ViewModel.ApplyCapturedHotkey(combo);
    }

    private void SwitchListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is WindowInfo window)
        {
            ViewModel.SwitchWindow(window);
        }
    }

    // ---- First-run walkthrough ----------------------------------------------------

    private sealed record OnboardingStep(Func<MainWindow, FrameworkElement?> Target, string Title, string Body);

    private static readonly OnboardingStep[] OnboardingSteps =
    {
        new(w => w.WindowListCard, "① 选择要镜像的窗口",
            "这里列出了当前所有可镜像的窗口，每 2 秒自动刷新。上方搜索框可以按标题或程序名快速过滤。"),
        new(w => w.MonitorListCard, "② 选择投放的显示器",
            "选中要把画面投到哪块屏幕。默认已经帮你选好副屏。注意双屏需要工作在「扩展」模式（Win + P → 扩展）。"),
        new(w => w.HotkeyButton, "③ 设置顺手的快捷键",
            "点击可以改成你习惯的组合键，讲课途中不用切回来点按钮。默认是 Ctrl+Alt+M。"),
        new(w => w.StartButton, "④ 开始镜像",
            "点击后副屏立刻全屏显示所选窗口，主屏可以继续自由操作。再按一次快捷键即可停止。"),
    };

    private int _onboardingIndex;

    private void StartOnboarding()
    {
        _onboardingIndex = 0;
        OnboardingOverlay.Visibility = Visibility.Visible;
        OnboardingOverlay.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        ShowOnboardingStep();
    }

    private void ShowOnboardingStep()
    {
        var step = OnboardingSteps[_onboardingIndex];
        var target = step.Target(this);

        OnboardingStepLabel.Text = $"第 {_onboardingIndex + 1} / {OnboardingSteps.Length} 步";
        OnboardingTitle.Text = step.Title;
        OnboardingBody.Text = step.Body;
        OnboardingNext.Content = _onboardingIndex == OnboardingSteps.Length - 1 ? "开始使用" : "下一步";

        if (target == null || !target.IsVisible)
        {
            OnboardingHighlight.Visibility = Visibility.Collapsed;
            UpdateOnboardingSpotlight();
            PlaceOnboardingCard(null);
            return;
        }

        // Target bounds in window coordinates.
        var topLeft = target.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
        var bounds = new Rect(topLeft, new System.Windows.Size(target.ActualWidth, target.ActualHeight));
        bounds.Inflate(6, 6);

        OnboardingHighlight.Visibility = Visibility.Visible;
        AnimateHighlightTo(bounds);
        PlaceOnboardingCard(bounds);
    }

    private void UpdateOnboardingSpotlight()
    {
        if (OnboardingOverlay.ActualWidth <= 0 || OnboardingOverlay.ActualHeight <= 0)
            return;

        var dimmerGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        dimmerGeometry.Children.Add(new RectangleGeometry(
            new Rect(0, 0, OnboardingOverlay.ActualWidth, OnboardingOverlay.ActualHeight)));

        if (OnboardingHighlight.Visibility == Visibility.Visible &&
            OnboardingHighlight.ActualWidth > 0 &&
            OnboardingHighlight.ActualHeight > 0)
        {
            var topLeft = OnboardingHighlight.TranslatePoint(
                new System.Windows.Point(0, 0), OnboardingOverlay);
            var spotlight = new Rect(
                topLeft,
                new System.Windows.Size(
                    OnboardingHighlight.ActualWidth,
                    OnboardingHighlight.ActualHeight));
            dimmerGeometry.Children.Add(new RectangleGeometry(spotlight, 12, 12));
        }

        OnboardingDimmer.Data = dimmerGeometry;
    }

    private void AnimateHighlightTo(Rect bounds)
    {
        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // First step has nothing to move from, so snap into place instead of sliding from 0,0.
        if (OnboardingHighlight.Width == 0 || double.IsNaN(OnboardingHighlight.Width))
        {
            OnboardingHighlight.Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
            OnboardingHighlight.Width = bounds.Width;
            OnboardingHighlight.Height = bounds.Height;
            return;
        }

        OnboardingHighlight.BeginAnimation(MarginProperty, new ThicknessAnimation(
            new Thickness(bounds.X, bounds.Y, 0, 0), duration) { EasingFunction = ease });
        OnboardingHighlight.BeginAnimation(WidthProperty, new DoubleAnimation(
            bounds.Width, duration) { EasingFunction = ease });
        OnboardingHighlight.BeginAnimation(HeightProperty, new DoubleAnimation(
            bounds.Height, duration) { EasingFunction = ease });
    }

    /// <summary>Puts the explanation card below the highlighted control, or above it near the bottom.</summary>
    private void PlaceOnboardingCard(Rect? highlight)
    {
        OnboardingCard.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double cardW = Math.Min(OnboardingCard.DesiredSize.Width, OnboardingCard.MaxWidth);
        double cardH = OnboardingCard.DesiredSize.Height;
        const double gap = 14;

        double x, y;
        if (highlight is { } r)
        {
            x = r.X + (r.Width - cardW) / 2;
            y = r.Bottom + gap;
            if (y + cardH > ActualHeight - 12) y = r.Y - cardH - gap;
        }
        else
        {
            x = (ActualWidth - cardW) / 2;
            y = (ActualHeight - cardH) / 2;
        }

        x = Math.Clamp(x, 12, Math.Max(12, ActualWidth - cardW - 12));
        y = Math.Clamp(y, 12, Math.Max(12, ActualHeight - cardH - 12));

        OnboardingCard.BeginAnimation(MarginProperty, new ThicknessAnimation(
            new Thickness(x, y, 0, 0), TimeSpan.FromMilliseconds(260))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
    }

    private void OnboardingNext_Click(object sender, RoutedEventArgs e)
    {
        if (_onboardingIndex >= OnboardingSteps.Length - 1)
        {
            FinishOnboarding();
            return;
        }

        _onboardingIndex++;
        ShowOnboardingStep();
    }

    private void OnboardingSkip_Click(object sender, RoutedEventArgs e) => FinishOnboarding();

    private void FinishOnboarding()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) => OnboardingOverlay.Visibility = Visibility.Collapsed;
        OnboardingOverlay.BeginAnimation(OpacityProperty, fade);
        ViewModel.CompleteOnboarding();
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
