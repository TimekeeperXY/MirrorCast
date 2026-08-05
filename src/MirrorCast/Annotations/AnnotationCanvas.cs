using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace MirrorCast.Annotations;

public sealed class AnnotationCanvas : FrameworkElement
{
    private AnnotationDocument? _document;
    private readonly List<AnnotationPoint> _draftPoints = [];
    private readonly HashSet<AnnotationItem> _erasedItems = [];
    private bool _isDrawing;
    private bool _isErasing;
    private Rect _viewRect = new(0, 0, 1, 1);

    public AnnotationDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;
            if (_document != null) _document.Changed -= OnDocumentChanged;
            _document = value;
            if (_document != null) _document.Changed += OnDocumentChanged;
            InvalidateVisual();
        }
    }

    public AnnotationTool Tool { get; set; } = AnnotationTool.Pen;
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeThickness { get; set; } = 4;
    public bool IsEditing { get; set; }

    public Rect ViewRect
    {
        get => _viewRect;
        set
        {
            var safe = new Rect(
                Math.Clamp(value.X, 0, 1),
                Math.Clamp(value.Y, 0, 1),
                Math.Clamp(value.Width, 0.0001, 1),
                Math.Clamp(value.Height, 0.0001, 1));
            if (_viewRect == safe) return;
            _viewRect = safe;
            InvalidateVisual();
        }
    }

    public AnnotationCanvas()
    {
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        Focusable = true;
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        => IsEditing ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEditing || Document == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        Focus();
        var point = e.GetPosition(this);
        if (Tool == AnnotationTool.Eraser)
        {
            _erasedItems.Clear();
            _isErasing = true;
            CaptureMouse();
            EraseAt(point);
            e.Handled = true;
            return;
        }

        _draftPoints.Clear();
        _draftPoints.Add(ToNormalized(point));
        _isDrawing = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isErasing && e.LeftButton == MouseButtonState.Pressed)
        {
            EraseAt(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (!_isDrawing || e.LeftButton != MouseButtonState.Pressed) return;

        var normalized = ToNormalized(e.GetPosition(this));
        if (Tool is AnnotationTool.Pen or AnnotationTool.Highlighter)
        {
            var last = _draftPoints[^1];
            if (Math.Abs(last.X - normalized.X) * ActualWidth < 1.5 &&
                Math.Abs(last.Y - normalized.Y) * ActualHeight < 1.5)
                return;
            _draftPoints.Add(normalized);
        }
        else if (_draftPoints.Count == 1)
        {
            _draftPoints.Add(normalized);
        }
        else
        {
            _draftPoints[^1] = normalized;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isErasing)
        {
            Document?.RemoveRange(_erasedItems);
            _erasedItems.Clear();
            _isErasing = false;
            ReleaseMouseCapture();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!_isDrawing || Document == null) return;

        var end = ToNormalized(e.GetPosition(this));
        if (_draftPoints.Count == 1) _draftPoints.Add(end);
        else if (Tool is not AnnotationTool.Pen and not AnnotationTool.Highlighter) _draftPoints[^1] = end;

        var opacity = Tool == AnnotationTool.Highlighter ? 0.38 : 1.0;
        var thickness = Tool == AnnotationTool.Highlighter ? StrokeThickness * 3 : StrokeThickness;
        Document.Add(new AnnotationItem(Tool, _draftPoints.ToArray(), StrokeColor, thickness, opacity));

        _draftPoints.Clear();
        _isDrawing = false;
        ReleaseMouseCapture();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Document != null)
        {
            foreach (var item in Document.Items)
            {
                if (!_erasedItems.Contains(item))
                    DrawItem(drawingContext, item);
            }
        }

        if (_draftPoints.Count > 0 && Tool != AnnotationTool.Eraser)
        {
            var opacity = Tool == AnnotationTool.Highlighter ? 0.38 : 1.0;
            var thickness = Tool == AnnotationTool.Highlighter ? StrokeThickness * 3 : StrokeThickness;
            DrawItem(drawingContext, new AnnotationItem(Tool, _draftPoints, StrokeColor, thickness, opacity));
        }
    }

    private void DrawItem(DrawingContext context, AnnotationItem item)
    {
        if (item.Points.Count == 0) return;
        var brush = new SolidColorBrush(item.Color) { Opacity = item.Opacity };
        brush.Freeze();
        var pen = new Pen(brush, item.Thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        var points = item.Points.Select(ToDisplay).ToArray();
        switch (item.Tool)
        {
            case AnnotationTool.Pen:
            case AnnotationTool.Highlighter:
                if (points.Length == 1)
                    context.DrawEllipse(brush, null, points[0], item.Thickness / 2, item.Thickness / 2);
                else
                    DrawPolyline(context, pen, points);
                break;
            case AnnotationTool.Line:
                if (points.Length >= 2) context.DrawLine(pen, points[0], points[^1]);
                break;
            case AnnotationTool.Arrow:
                if (points.Length >= 2) DrawArrow(context, pen, points[0], points[^1]);
                break;
            case AnnotationTool.Rectangle:
                if (points.Length >= 2) context.DrawRectangle(null, pen, MakeRect(points[0], points[^1]));
                break;
            case AnnotationTool.Ellipse:
                if (points.Length >= 2)
                {
                    var rect = MakeRect(points[0], points[^1]);
                    context.DrawEllipse(null, pen,
                        new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                        rect.Width / 2, rect.Height / 2);
                }
                break;
        }
    }

    private static void DrawPolyline(DrawingContext context, Pen pen, IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            ctx.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawArrow(DrawingContext context, Pen pen, Point start, Point end)
    {
        context.DrawLine(pen, start, end);
        var vector = start - end;
        if (vector.Length < 1) return;
        vector.Normalize();
        var perpendicular = new Vector(-vector.Y, vector.X);
        double length = Math.Clamp(pen.Thickness * 4, 12, 28);
        context.DrawLine(pen, end, end + vector * length + perpendicular * length * 0.45);
        context.DrawLine(pen, end, end + vector * length - perpendicular * length * 0.45);
    }

    private AnnotationItem? FindHit(Point point, double tolerance)
    {
        if (Document == null) return null;
        for (int i = Document.Items.Count - 1; i >= 0; i--)
        {
            var item = Document.Items[i];
            if (_erasedItems.Contains(item)) continue;
            var points = item.Points.Select(ToDisplay).ToArray();
            if (points.Length == 0) continue;
            double hitTolerance = tolerance + item.Thickness / 2;

            if (item.Tool is AnnotationTool.Rectangle && points.Length >= 2)
            {
                var rect = MakeRect(points[0], points[^1]);
                var corners = new[]
                {
                    rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft, rect.TopLeft
                };
                if (HasNearbySegment(point, corners, hitTolerance)) return item;
                continue;
            }

            if (item.Tool is AnnotationTool.Ellipse && points.Length >= 2)
            {
                var rect = MakeRect(points[0], points[^1]);
                if (DistanceToEllipse(point, rect) <= hitTolerance) return item;
                continue;
            }

            if (points.Length == 1 && (point - points[0]).Length <= hitTolerance) return item;
            if (HasNearbySegment(point, points, hitTolerance)) return item;
        }
        return null;
    }

    private void EraseAt(Point point)
    {
        var hit = FindHit(point, 14);
        if (hit == null) return;
        _erasedItems.Add(hit);
        InvalidateVisual();
    }

    private static bool HasNearbySegment(Point point, IReadOnlyList<Point> points, double tolerance)
    {
        for (int i = 1; i < points.Count; i++)
        {
            if (DistanceToSegment(point, points[i - 1], points[i]) <= tolerance)
                return true;
        }
        return false;
    }

    private static double DistanceToEllipse(Point point, Rect rect)
    {
        double radiusX = rect.Width / 2;
        double radiusY = rect.Height / 2;
        if (radiusX < 0.5 || radiusY < 0.5)
            return DistanceToSegment(point, rect.TopLeft, rect.BottomRight);

        double normalizedX = (point.X - (rect.Left + radiusX)) / radiusX;
        double normalizedY = (point.Y - (rect.Top + radiusY)) / radiusY;
        double normalizedDistance = Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
        return Math.Abs(normalizedDistance - 1) * Math.Min(radiusX, radiusY);
    }

    private AnnotationPoint ToNormalized(Point point)
        => new(
            Math.Clamp(point.X / Math.Max(1, ActualWidth), 0, 1),
            Math.Clamp(point.Y / Math.Max(1, ActualHeight), 0, 1));

    private Point ToDisplay(AnnotationPoint point)
        => new(
            (point.X - ViewRect.X) / ViewRect.Width * ActualWidth,
            (point.Y - ViewRect.Y) / ViewRect.Height * ActualHeight);

    private static Rect MakeRect(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        if (ab.LengthSquared < 0.001) return (p - a).Length;
        double t = Math.Clamp(Vector.Multiply(p - a, ab) / ab.LengthSquared, 0, 1);
        return (p - (a + ab * t)).Length;
    }

    private void OnDocumentChanged() => InvalidateVisual();
}
