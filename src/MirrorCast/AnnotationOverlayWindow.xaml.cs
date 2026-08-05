using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MirrorCast.Annotations;
using MirrorCast.Interop;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MirrorCast;

public partial class AnnotationOverlayWindow : Window
{
    private IntPtr _hwnd;
    private AnnotationDocument? _document;

    public event Action? ExitRequested;

    public AnnotationOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        UpdateToolButtons();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        long exStyle = User32.GetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle |= User32.WS_EX_TOOLWINDOW;
        User32.SetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    public void SetDocument(AnnotationDocument document)
    {
        if (_document != null) _document.Changed -= OnDocumentChanged;
        _document = document;
        _document.Changed += OnDocumentChanged;
        DrawingSurface.Document = document;
        OnDocumentChanged();
    }

    public void SetBounds(RECT bounds)
    {
        if (_hwnd == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0) return;
        User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            User32.SWP_SHOWWINDOW);
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AnnotationTool tool }) return;
        DrawingSurface.Tool = tool;
        DrawingSurface.Cursor = tool == AnnotationTool.Eraser ? Cursors.Arrow : Cursors.Cross;
        UpdateToolButtons();
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string colorText }) return;
        DrawingSurface.StrokeColor = (Color)ColorConverter.ConvertFromString(colorText);
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DrawingSurface != null) DrawingSurface.StrokeThickness = e.NewValue;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => _document?.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => _document?.Redo();
    private void Clear_Click(object sender, RoutedEventArgs e) => _document?.Clear();
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ExitRequested?.Invoke();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Z)
        {
            _document?.Undo();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Y)
        {
            _document?.Redo();
            e.Handled = true;
        }
    }

    private void UpdateToolButtons()
    {
        var buttons = new[]
        {
            PenButton, HighlighterButton, LineButton, ArrowButton,
            RectangleButton, EllipseButton, EraserButton
        };
        foreach (var button in buttons)
        {
            button.Background = button.Tag is AnnotationTool tool && tool == DrawingSurface.Tool
                ? new SolidColorBrush(Color.FromRgb(91, 110, 245))
                : new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
        }
    }

    private void OnDocumentChanged()
    {
        if (_document == null) return;
        UndoButton.IsEnabled = _document.CanUndo;
        RedoButton.IsEnabled = _document.CanRedo;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_document != null) _document.Changed -= OnDocumentChanged;
        base.OnClosed(e);
    }
}
