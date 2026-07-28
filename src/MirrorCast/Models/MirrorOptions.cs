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

    /// <summary>
    /// Flashes a ripple on the mirrored picture wherever the presenter clicks. The mirrored
    /// frame only ever shows the *result* of a click, so without this the audience cannot
    /// tell what was clicked or even that a click happened.
    /// </summary>
    public bool ShowClickEffects { get; set; } = true;
}
