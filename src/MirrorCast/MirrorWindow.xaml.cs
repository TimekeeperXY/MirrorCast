using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MirrorCast;

public partial class MirrorWindow : Window
{
    public event Action? EscapePressed;

    private bool _hideCursor;
    public bool HideCursor
    {
        get => _hideCursor;
        set
        {
            _hideCursor = value;
            Cursor = value ? System.Windows.Input.Cursors.None : System.Windows.Input.Cursors.Arrow;
        }
    }

    public MirrorWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            EscapePressed?.Invoke();
            e.Handled = true;
        }
    }

    public void SetMinimizedOverlay(bool visible)
    {
        OverlayText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowCursorOverlay(BitmapSource bitmap, double left, double top, double width, double height)
    {
        CursorImage.Source = bitmap;
        Canvas.SetLeft(CursorImage, left);
        Canvas.SetTop(CursorImage, top);
        CursorImage.Width = width;
        CursorImage.Height = height;
        CursorImage.Visibility = Visibility.Visible;
    }

    public void HideCursorOverlay()
    {
        CursorImage.Visibility = Visibility.Collapsed;
    }

    public (double X, double Y) GetDpiScale()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return (dpi.DpiScaleX, dpi.DpiScaleY);
    }
}
