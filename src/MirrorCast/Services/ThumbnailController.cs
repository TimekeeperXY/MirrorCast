using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MirrorCast.Interop;
using MirrorCast.Models;

namespace MirrorCast.Services;

public class ThumbnailController
{
    private IntPtr _thumbnail = IntPtr.Zero;
    private MirrorWindow? _mirrorWindow;
    private CursorOverlayWindow? _cursorWindow;
    private IntPtr _destHwnd;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _cursorTimer;
    private IntPtr _sourceHwnd;
    private MonitorInfo? _targetMonitor;
    private MirrorOptions _options = new();
    private bool _wasMinimized;
    private RECT _lastDestRect;
    private SIZE _lastSourceSize;

    public event Action? SourceClosed;
    public event Action? TargetMonitorLost;
    public event Action? StoppedByUser;

    public bool IsRunning => _thumbnail != IntPtr.Zero;

    public void Start(IntPtr sourceHwnd, MonitorInfo target, MirrorOptions options)
    {
        Stop();

        _sourceHwnd = sourceHwnd;
        _targetMonitor = target;
        _options = options;
        _wasMinimized = false;
        _lastSourceSize = default;

        _mirrorWindow = new MirrorWindow
        {
            HideCursor = options.HideCursor,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000,
            Width = 200,
            Height = 200
        };
        _mirrorWindow.EscapePressed += () => StoppedByUser?.Invoke();
        _mirrorWindow.Show();

        _destHwnd = new WindowInteropHelper(_mirrorWindow).Handle;

        User32.SetWindowPos(_destHwnd, User32.HWND_TOPMOST,
            target.Bounds.Left, target.Bounds.Top, target.Bounds.Width, target.Bounds.Height,
            User32.SWP_SHOWWINDOW);

        int hr = DwmApi.DwmRegisterThumbnail(_destHwnd, sourceHwnd, out _thumbnail);
        if (hr != 0)
        {
            _mirrorWindow.Close();
            _mirrorWindow = null;
            _thumbnail = IntPtr.Zero;
            throw new InvalidOperationException($"DwmRegisterThumbnail 调用失败 (hr=0x{hr:X8})");
        }

        ApplyProperties();

        // Must be a separate top-level window — DWM draws the thumbnail over the
        // mirror window's own content, so an in-window overlay is never visible.
        _cursorWindow = new CursorOverlayWindow();
        _cursorWindow.Show();
        _cursorWindow.HideCursor();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _cursorTimer.Tick += (_, _) =>
        {
            ResyncIfSourceSizeChanged();
            UpdateCursorOverlay();
        };
        _cursorTimer.Start();
    }

    /// <summary>
    /// Swaps the mirrored source window in place, keeping the mirror window, target monitor
    /// and timers alive so the switch is seamless (no flicker / no re-show of the mirror window).
    /// </summary>
    public void SwitchSource(IntPtr newSourceHwnd)
    {
        if (_mirrorWindow == null || _targetMonitor == null) return;
        if (newSourceHwnd == _sourceHwnd) return;

        if (_thumbnail != IntPtr.Zero)
        {
            DwmApi.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = IntPtr.Zero;
        }

        int hr = DwmApi.DwmRegisterThumbnail(_destHwnd, newSourceHwnd, out _thumbnail);
        if (hr != 0)
        {
            _thumbnail = IntPtr.Zero;
            throw new InvalidOperationException($"DwmRegisterThumbnail 调用失败 (hr=0x{hr:X8})");
        }

        _sourceHwnd = newSourceHwnd;
        _wasMinimized = false;
        _mirrorWindow.SetMinimizedOverlay(false);

        ApplyProperties();
    }

    private void Tick()
    {
        if (_thumbnail == IntPtr.Zero || _targetMonitor == null) return;

        if (!User32.IsWindow(_sourceHwnd))
        {
            SourceClosed?.Invoke();
            return;
        }

        var monitors = MonitorEnumerator.EnumerateMonitors();
        if (monitors.All(m => m.DeviceName != _targetMonitor.DeviceName))
        {
            TargetMonitorLost?.Invoke();
            return;
        }

        bool isMinimized = User32.IsIconic(_sourceHwnd);
        if (isMinimized != _wasMinimized)
        {
            _wasMinimized = isMinimized;
            _mirrorWindow?.SetMinimizedOverlay(isMinimized);
        }

        ApplyProperties();
    }

    /// <summary>
    /// Cheap high-frequency check so a live resize drag on the source window is followed
    /// immediately, instead of the picture being stretched into a stale rect for up to
    /// one slow-timer interval.
    /// </summary>
    private void ResyncIfSourceSizeChanged()
    {
        if (_thumbnail == IntPtr.Zero) return;
        if (User32.IsIconic(_sourceHwnd)) return; // handled by the slow tick
        if (DwmApi.DwmQueryThumbnailSourceSize(_thumbnail, out var size) != 0) return;
        if (size.cx == _lastSourceSize.cx && size.cy == _lastSourceSize.cy) return;

        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (_thumbnail == IntPtr.Zero || _targetMonitor == null) return;

        // While the source is minimized the thumbnail keeps painting a frozen last frame,
        // and it covers this window's own content — so the "source minimized" notice would
        // never be seen. Hide the thumbnail so that notice shows instead of a stale image.
        // Checked before the size guard because a minimized source can report a zero size.
        if (User32.IsIconic(_sourceHwnd))
        {
            var hidden = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmApi.DWM_TNP_VISIBLE,
                fVisible = false
            };
            DwmApi.DwmUpdateThumbnailProperties(_thumbnail, ref hidden);
            return;
        }

        DwmApi.DwmQueryThumbnailSourceSize(_thumbnail, out var srcSize);
        if (srcSize.cx <= 0 || srcSize.cy <= 0) return;

        _lastSourceSize = srcSize;

        var destRect = CalculateDestRect(srcSize, _targetMonitor.Bounds.Width, _targetMonitor.Bounds.Height, _options.ScaleMode);
        _lastDestRect = destRect;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_OPACITY
                    | DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_SOURCECLIENTAREAONLY,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = _options.ClientAreaOnly,
            rcDestination = destRect
        };

        DwmApi.DwmUpdateThumbnailProperties(_thumbnail, ref props);
    }

    private static RECT CalculateDestRect(SIZE source, int destW, int destH, ScaleMode mode)
    {
        switch (mode)
        {
            case ScaleMode.Stretch:
                return new RECT { Left = 0, Top = 0, Right = destW, Bottom = destH };

            case ScaleMode.Original:
            {
                // DWM stretches the source into rcDestination rather than cropping it, so the
                // rect must keep the source's aspect ratio or the picture distorts. Show 1:1 when
                // the source fits, and shrink uniformly (never per-axis) when it doesn't.
                double scale = Math.Min(1.0, Math.Min((double)destW / source.cx, (double)destH / source.cy));
                int w = Math.Max(1, (int)Math.Round(source.cx * scale));
                int h = Math.Max(1, (int)Math.Round(source.cy * scale));
                int x = (destW - w) / 2;
                int y = (destH - h) / 2;
                return new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
            }

            case ScaleMode.Fit:
            default:
            {
                double sourceRatio = (double)source.cx / source.cy;
                double destRatio = (double)destW / destH;
                int w, h, x, y;
                if (sourceRatio > destRatio)
                {
                    w = destW;
                    h = (int)Math.Round(destW / sourceRatio);
                    x = 0;
                    y = (destH - h) / 2;
                }
                else
                {
                    h = destH;
                    w = (int)Math.Round(destH * sourceRatio);
                    x = (destW - w) / 2;
                    y = 0;
                }
                return new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
            }
        }
    }

    private void UpdateCursorOverlay()
    {
        if (_cursorWindow == null || _targetMonitor == null) return;

        if (!_options.ShowSyntheticCursor ||
            !User32.IsWindow(_sourceHwnd) ||
            User32.IsIconic(_sourceHwnd) ||
            !TryGetSourceScreenRect(_sourceHwnd, _options.ClientAreaOnly, out var sourceRect) ||
            sourceRect.Width <= 0 || sourceRect.Height <= 0 ||
            !User32.GetCursorPos(out var cursorPos) ||
            cursorPos.X < sourceRect.Left || cursorPos.X >= sourceRect.Right ||
            cursorPos.Y < sourceRect.Top || cursorPos.Y >= sourceRect.Bottom ||
            !CursorHelper.TryGetCurrentCursor(out var bitmap, out var hotspotX, out var hotspotY, out _) ||
            bitmap == null)
        {
            _cursorWindow.HideCursor();
            return;
        }

        // Map cursor position from the source window onto the mirrored rectangle.
        double relX = (double)(cursorPos.X - sourceRect.Left) / sourceRect.Width;
        double relY = (double)(cursorPos.Y - sourceRect.Top) / sourceRect.Height;

        // _lastDestRect is relative to the mirror window's client area, which is
        // positioned at the monitor's physical origin — so add it back for screen coords.
        double screenX = _targetMonitor.Bounds.Left + _lastDestRect.Left + relX * _lastDestRect.Width;
        double screenY = _targetMonitor.Bounds.Top + _lastDestRect.Top + relY * _lastDestRect.Height;

        // Scale the cursor the same way the picture is scaled, but keep it from
        // becoming unreadably small when the source is heavily downscaled.
        double scale = _lastDestRect.Width > 0 && sourceRect.Width > 0
            ? (double)_lastDestRect.Width / sourceRect.Width
            : 1.0;
        scale = Math.Clamp(scale, 0.75, 3.0);

        int w = Math.Max(1, (int)Math.Round(bitmap.PixelWidth * scale));
        int h = Math.Max(1, (int)Math.Round(bitmap.PixelHeight * scale));
        int x = (int)Math.Round(screenX - hotspotX * scale);
        int y = (int)Math.Round(screenY - hotspotY * scale);

        _cursorWindow.ShowCursor(bitmap, x, y, w, h, _destHwnd);
    }

    private static bool TryGetSourceScreenRect(IntPtr hwnd, bool clientAreaOnly, out RECT rect)
    {
        if (clientAreaOnly)
        {
            if (!User32.GetClientRect(hwnd, out var clientRect))
            {
                rect = default;
                return false;
            }

            var topLeft = new POINT { X = 0, Y = 0 };
            if (!User32.ClientToScreen(hwnd, ref topLeft))
            {
                rect = default;
                return false;
            }

            rect = new RECT
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = topLeft.X + clientRect.Width,
                Bottom = topLeft.Y + clientRect.Height
            };
            return true;
        }

        return User32.GetWindowRect(hwnd, out rect);
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        _cursorTimer?.Stop();
        _cursorTimer = null;

        if (_thumbnail != IntPtr.Zero)
        {
            DwmApi.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = IntPtr.Zero;
        }

        _cursorWindow?.Close();
        _cursorWindow = null;
        _mirrorWindow?.Close();
        _mirrorWindow = null;
        _destHwnd = IntPtr.Zero;
        _targetMonitor = null;
        _wasMinimized = false;
    }
}
