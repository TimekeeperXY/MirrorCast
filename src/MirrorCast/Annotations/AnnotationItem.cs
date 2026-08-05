using System.Windows.Media;

namespace MirrorCast.Annotations;

public readonly record struct AnnotationPoint(double X, double Y);

public sealed record AnnotationItem(
    AnnotationTool Tool,
    IReadOnlyList<AnnotationPoint> Points,
    System.Windows.Media.Color Color,
    double Thickness,
    double Opacity);
