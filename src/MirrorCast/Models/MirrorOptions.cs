namespace MirrorCast.Models;

public class MirrorOptions
{
    public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
    public bool ClientAreaOnly { get; set; } = true;

    /// <summary>Hides the real mouse pointer while it physically sits on the mirror display.</summary>
    public bool HideCursor { get; set; } = true;

    /// <summary>
    /// Draws a synthetic copy of the pointer on the mirrored picture, tracking where it is
    /// inside the source window. Separate from <see cref="HideCursor"/>: DWM thumbnails never
    /// include the pointer, so without this the audience cannot see what you are pointing at.
    /// </summary>
    public bool ShowSyntheticCursor { get; set; } = true;

    /// <summary>Magnification used by full-screen zoom and the pointer magnifier.</summary>
    public double PresentationZoomFactor { get; set; } = 2.0;

    /// <summary>Diameter of pointer-centered presentation effects in physical pixels.</summary>
    public int PointerEffectSize { get; set; } = 240;
}
