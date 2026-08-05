using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MirrorCast.Annotations;
using MirrorCast.Interop;
using MirrorCast.Models;

namespace MirrorCast;

/// <summary>Draws click-through spotlight dimming and the magnifier frame above DWM thumbnails.</summary>
public partial class PresentationOverlayWindow : Window
{
    private IntPtr _hwnd;
    private MonitorInfo? _monitor;

    public PresentationOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        long exStyle = User32.GetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle |= User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
        User32.SetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    public void SetMonitor(MonitorInfo monitor)
    {
        _monitor = monitor;
        if (_hwnd == IntPtr.Zero) return;
        User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST,
            monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height,
            User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    public void SetAnnotationDocument(AnnotationDocument document)
    {
        ProjectedAnnotations.Document = document;
    }

    public void UpdateAnnotationViewport(RECT screenBounds, Rect sourceView, bool visible)
    {
        if (_hwnd == IntPtr.Zero || screenBounds.Width <= 0 || screenBounds.Height <= 0) return;
        if (!visible)
        {
            ProjectedAnnotations.Visibility = Visibility.Collapsed;
            return;
        }

        var topLeft = PointFromScreen(new System.Windows.Point(screenBounds.Left, screenBounds.Top));
        var bottomRight = PointFromScreen(new System.Windows.Point(screenBounds.Right, screenBounds.Bottom));
        ProjectedAnnotations.Width = Math.Max(1, bottomRight.X - topLeft.X);
        ProjectedAnnotations.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
        ProjectedAnnotations.ViewRect = sourceView;
        Canvas.SetLeft(ProjectedAnnotations, topLeft.X);
        Canvas.SetTop(ProjectedAnnotations, topLeft.Y);
        ProjectedAnnotations.Visibility = Visibility.Visible;
    }

    public void UpdateEffects(bool spotlightVisible, double screenX, double screenY,
        int radius, RECT? magnifierBounds)
    {
        if (_monitor == null || _hwnd == IntPtr.Zero) return;

        var center = PointFromScreen(new System.Windows.Point(screenX, screenY));
        var radiusPoint = PointFromScreen(new System.Windows.Point(screenX + radius, screenY));
        double dipRadius = Math.Abs(radiusPoint.X - center.X);

        if (spotlightVisible)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            geometry.Children.Add(new EllipseGeometry(center, dipRadius, dipRadius));
            SpotlightDimmer.Data = geometry;
            SpotlightDimmer.Visibility = Visibility.Visible;
        }
        else
        {
            SpotlightDimmer.Visibility = Visibility.Collapsed;
        }

        if (magnifierBounds is { } bounds)
        {
            var topLeft = PointFromScreen(new System.Windows.Point(bounds.Left, bounds.Top));
            var bottomRight = PointFromScreen(new System.Windows.Point(bounds.Right, bounds.Bottom));
            MagnifierFrame.Width = Math.Max(1, bottomRight.X - topLeft.X);
            MagnifierFrame.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
            Canvas.SetLeft(MagnifierFrame, topLeft.X);
            Canvas.SetTop(MagnifierFrame, topLeft.Y);
            MagnifierFrame.Visibility = Visibility.Visible;
        }
        else
        {
            MagnifierFrame.Visibility = Visibility.Collapsed;
        }

        User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW);
    }

    public void ClearEffects()
    {
        SpotlightDimmer.Visibility = Visibility.Collapsed;
        MagnifierFrame.Visibility = Visibility.Collapsed;
    }
}
