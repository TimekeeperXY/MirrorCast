using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MirrorCast.Interop;
using MirrorCast.Models;

namespace MirrorCast.Services;

public class ThumbnailController
{
    private IntPtr _thumbnail = IntPtr.Zero;
    private IntPtr _magnifierThumbnail = IntPtr.Zero;
    private MirrorWindow? _mirrorWindow;
    private CursorOverlayWindow? _cursorWindow;
    private MagnifierWindow? _magnifierWindow;
    private PresentationOverlayWindow? _presentationOverlay;
    private IntPtr _destHwnd;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _cursorTimer;
    private IntPtr _sourceHwnd;
    private MonitorInfo? _targetMonitor;
    private MirrorOptions _options = new();
    private bool _wasMinimized;
    private RECT _lastDestRect;
    private RECT _lastSourceCrop;
    private SIZE _lastSourceSize;
    private bool _screenZoomEnabled;
    private bool _magnifierEnabled;
    private bool _spotlightEnabled;
    private bool _pointerSourceValid;
    private double _pointerSourceX;
    private double _pointerSourceY;

    public event Action? SourceClosed;
    public event Action? TargetMonitorLost;
    public event Action? StoppedByUser;

    public bool IsRunning => _thumbnail != IntPtr.Zero;
    public bool IsScreenZoomEnabled => _screenZoomEnabled;
    public bool IsMagnifierEnabled => _magnifierEnabled;
    public bool IsSpotlightEnabled => _spotlightEnabled;

    public void Start(IntPtr sourceHwnd, MonitorInfo target, MirrorOptions options)
    {
        Stop();

        _sourceHwnd = sourceHwnd;
        _targetMonitor = target;
        _options = options;
        _wasMinimized = false;
        _lastSourceSize = default;
        _lastSourceCrop = default;
        _screenZoomEnabled = false;
        _magnifierEnabled = false;
        _spotlightEnabled = false;
        _pointerSourceValid = false;

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

        _magnifierWindow = new MagnifierWindow();
        _magnifierWindow.Show();
        _magnifierWindow.HideMagnifier();

        _presentationOverlay = new PresentationOverlayWindow();
        _presentationOverlay.Show();
        _presentationOverlay.SetMonitor(target);
        _presentationOverlay.ClearEffects();

        // Must be a separate top-level window because DWM draws over the mirror window.
        // Create it last so the synthetic pointer stays above presentation effects.
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
            UpdatePresentationEffects();
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

        UnregisterMagnifierThumbnail();

        int hr = DwmApi.DwmRegisterThumbnail(_destHwnd, newSourceHwnd, out _thumbnail);
        if (hr != 0)
        {
            _thumbnail = IntPtr.Zero;
            throw new InvalidOperationException($"DwmRegisterThumbnail 调用失败 (hr=0x{hr:X8})");
        }

        _sourceHwnd = newSourceHwnd;
        _wasMinimized = false;
        _pointerSourceValid = false;
        _mirrorWindow.SetMinimizedOverlay(false);

        ApplyProperties();
        if (_magnifierEnabled) EnsureMagnifierThumbnail();
    }

    public bool ToggleScreenZoom()
    {
        if (!IsRunning) return false;

        _screenZoomEnabled = !_screenZoomEnabled;
        if (_screenZoomEnabled)
        {
            _magnifierEnabled = false;
            HideMagnifier();
        }

        ApplyProperties();
        return _screenZoomEnabled;
    }

    public bool ToggleMagnifier()
    {
        if (!IsRunning) return false;

        _magnifierEnabled = !_magnifierEnabled;
        if (_magnifierEnabled)
        {
            _screenZoomEnabled = false;
            ApplyProperties();
            EnsureMagnifierThumbnail();
        }
        else
        {
            HideMagnifier();
        }

        return _magnifierEnabled;
    }

    public bool ToggleSpotlight()
    {
        if (!IsRunning) return false;
        _spotlightEnabled = !_spotlightEnabled;
        if (!_spotlightEnabled && !_magnifierEnabled)
            _presentationOverlay?.ClearEffects();
        return _spotlightEnabled;
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

        _lastSourceCrop = _screenZoomEnabled
            ? CalculateCenteredCrop(srcSize,
                _pointerSourceValid ? _pointerSourceX : srcSize.cx / 2.0,
                _pointerSourceValid ? _pointerSourceY : srcSize.cy / 2.0,
                _options.PresentationZoomFactor)
            : new RECT { Left = 0, Top = 0, Right = srcSize.cx, Bottom = srcSize.cy };

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_OPACITY
                    | DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_SOURCECLIENTAREAONLY,
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = _options.ClientAreaOnly,
            rcDestination = destRect
        };

        if (_screenZoomEnabled)
        {
            props.dwFlags |= DwmApi.DWM_TNP_RECTSOURCE;
            props.rcSource = _lastSourceCrop;
        }

        DwmApi.DwmUpdateThumbnailProperties(_thumbnail, ref props);
    }

    private static RECT CalculateCenteredCrop(SIZE source, double centerX, double centerY, double zoomFactor)
    {
        zoomFactor = Math.Clamp(zoomFactor, 1.25, 5.0);
        int width = Math.Clamp((int)Math.Round(source.cx / zoomFactor), 1, source.cx);
        int height = Math.Clamp((int)Math.Round(source.cy / zoomFactor), 1, source.cy);
        int left = Math.Clamp((int)Math.Round(centerX - width / 2.0), 0, source.cx - width);
        int top = Math.Clamp((int)Math.Round(centerY - height / 2.0), 0, source.cy - height);
        return new RECT { Left = left, Top = top, Right = left + width, Bottom = top + height };
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

    private void UpdatePresentationEffects()
    {
        if (_targetMonitor == null || _presentationOverlay == null) return;

        if (!TryGetPointerSource(out var sourceX, out var sourceY))
        {
            _pointerSourceValid = false;
            HideMagnifier();
            _presentationOverlay.ClearEffects();
            return;
        }

        _pointerSourceValid = true;
        _pointerSourceX = sourceX;
        _pointerSourceY = sourceY;

        if (_screenZoomEnabled)
            ApplyProperties();

        if (!_magnifierEnabled && !_spotlightEnabled)
        {
            _presentationOverlay.ClearEffects();
            return;
        }

        if (!TryProjectSourcePoint(sourceX, sourceY, out var screenX, out var screenY))
            return;

        RECT? magnifierBounds = null;
        if (_magnifierEnabled)
            magnifierBounds = UpdateMagnifier(sourceX, sourceY, screenX, screenY);
        else
            HideMagnifier();

        _presentationOverlay.UpdateEffects(
            _spotlightEnabled,
            screenX,
            screenY,
            Math.Max(40, _options.PointerEffectSize / 2),
            magnifierBounds);
    }

    private void UpdateCursorOverlay()
    {
        if (_cursorWindow == null || _targetMonitor == null) return;

        if (!_options.ShowSyntheticCursor ||
            !User32.IsWindow(_sourceHwnd) ||
            User32.IsIconic(_sourceHwnd) ||
            !TryGetPointerSource(out var sourceX, out var sourceY) ||
            !TryProjectSourcePoint(sourceX, sourceY, out var screenX, out var screenY) ||
            !CursorHelper.TryGetCurrentCursor(out var bitmap, out var hotspotX, out var hotspotY, out _) ||
            bitmap == null)
        {
            _cursorWindow.HideCursor();
            return;
        }

        // Scale the cursor the same way the picture is scaled, but keep it from
        // becoming unreadably small when the source is heavily downscaled.
        double scale = _lastDestRect.Width > 0 && _lastSourceCrop.Width > 0
            ? (double)_lastDestRect.Width / _lastSourceCrop.Width
            : 1.0;
        scale = Math.Clamp(scale, 0.75, 3.0);

        int w = Math.Max(1, (int)Math.Round(bitmap.PixelWidth * scale));
        int h = Math.Max(1, (int)Math.Round(bitmap.PixelHeight * scale));
        int x = (int)Math.Round(screenX - hotspotX * scale);
        int y = (int)Math.Round(screenY - hotspotY * scale);

        _cursorWindow.ShowCursor(bitmap, x, y, w, h, _destHwnd);
    }

    private bool TryGetPointerSource(out double sourceX, out double sourceY)
    {
        sourceX = 0;
        sourceY = 0;
        if (User32.IsIconic(_sourceHwnd) ||
            _lastSourceSize.cx <= 0 || _lastSourceSize.cy <= 0 ||
            !TryGetSourceScreenRect(_sourceHwnd, _options.ClientAreaOnly, out var sourceRect) ||
            sourceRect.Width <= 0 || sourceRect.Height <= 0 ||
            !User32.GetCursorPos(out var cursorPos) ||
            cursorPos.X < sourceRect.Left || cursorPos.X >= sourceRect.Right ||
            cursorPos.Y < sourceRect.Top || cursorPos.Y >= sourceRect.Bottom)
        {
            return false;
        }

        sourceX = (double)(cursorPos.X - sourceRect.Left) / sourceRect.Width * _lastSourceSize.cx;
        sourceY = (double)(cursorPos.Y - sourceRect.Top) / sourceRect.Height * _lastSourceSize.cy;
        return true;
    }

    private bool TryProjectSourcePoint(double sourceX, double sourceY, out double screenX, out double screenY)
    {
        screenX = 0;
        screenY = 0;
        if (_targetMonitor == null || _lastSourceCrop.Width <= 0 || _lastSourceCrop.Height <= 0)
            return false;

        double relX = (sourceX - _lastSourceCrop.Left) / _lastSourceCrop.Width;
        double relY = (sourceY - _lastSourceCrop.Top) / _lastSourceCrop.Height;
        screenX = _targetMonitor.Bounds.Left + _lastDestRect.Left + relX * _lastDestRect.Width;
        screenY = _targetMonitor.Bounds.Top + _lastDestRect.Top + relY * _lastDestRect.Height;
        return true;
    }

    private RECT? UpdateMagnifier(double sourceX, double sourceY, double screenX, double screenY)
    {
        if (_targetMonitor == null || _magnifierWindow == null || !EnsureMagnifierThumbnail())
            return null;

        int size = Math.Clamp(_options.PointerEffectSize, 120, 480);
        int margin = 8;
        int x = Math.Clamp((int)Math.Round(screenX - size / 2.0),
            _targetMonitor.Bounds.Left + margin,
            _targetMonitor.Bounds.Right - size - margin);
        int y = Math.Clamp((int)Math.Round(screenY - size / 2.0),
            _targetMonitor.Bounds.Top + margin,
            _targetMonitor.Bounds.Bottom - size - margin);
        _magnifierWindow.ShowAt(x, y, size);

        double displayScaleX = _lastDestRect.Width > 0 && _lastSourceCrop.Width > 0
            ? (double)_lastDestRect.Width / _lastSourceCrop.Width : 1.0;
        double displayScaleY = _lastDestRect.Height > 0 && _lastSourceCrop.Height > 0
            ? (double)_lastDestRect.Height / _lastSourceCrop.Height : 1.0;
        double factor = Math.Clamp(_options.PresentationZoomFactor, 1.25, 5.0);
        int cropWidth = Math.Clamp((int)Math.Round(size / Math.Max(0.01, displayScaleX * factor)), 1, _lastSourceSize.cx);
        int cropHeight = Math.Clamp((int)Math.Round(size / Math.Max(0.01, displayScaleY * factor)), 1, _lastSourceSize.cy);
        int cropLeft = Math.Clamp((int)Math.Round(sourceX - cropWidth / 2.0), 0, _lastSourceSize.cx - cropWidth);
        int cropTop = Math.Clamp((int)Math.Round(sourceY - cropHeight / 2.0), 0, _lastSourceSize.cy - cropHeight);

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_OPACITY |
                      DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_RECTSOURCE |
                      DwmApi.DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new RECT { Left = 0, Top = 0, Right = size, Bottom = size },
            rcSource = new RECT { Left = cropLeft, Top = cropTop, Right = cropLeft + cropWidth, Bottom = cropTop + cropHeight },
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = _options.ClientAreaOnly
        };
        DwmApi.DwmUpdateThumbnailProperties(_magnifierThumbnail, ref props);

        return new RECT { Left = x, Top = y, Right = x + size, Bottom = y + size };
    }

    private bool EnsureMagnifierThumbnail()
    {
        if (_magnifierThumbnail != IntPtr.Zero) return true;
        if (_magnifierWindow?.Handle == IntPtr.Zero) return false;
        return DwmApi.DwmRegisterThumbnail(_magnifierWindow!.Handle, _sourceHwnd, out _magnifierThumbnail) == 0;
    }

    private void HideMagnifier()
    {
        _magnifierWindow?.HideMagnifier();
    }

    private void UnregisterMagnifierThumbnail()
    {
        if (_magnifierThumbnail == IntPtr.Zero) return;
        DwmApi.DwmUnregisterThumbnail(_magnifierThumbnail);
        _magnifierThumbnail = IntPtr.Zero;
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

        UnregisterMagnifierThumbnail();

        _cursorWindow?.Close();
        _cursorWindow = null;
        _presentationOverlay?.Close();
        _presentationOverlay = null;
        _magnifierWindow?.Close();
        _magnifierWindow = null;
        _mirrorWindow?.Close();
        _mirrorWindow = null;
        _destHwnd = IntPtr.Zero;
        _targetMonitor = null;
        _wasMinimized = false;
        _screenZoomEnabled = false;
        _magnifierEnabled = false;
        _spotlightEnabled = false;
        _pointerSourceValid = false;
    }
}
