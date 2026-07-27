namespace MirrorCast.Models;

public class MirrorOptions
{
    public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
    public bool ClientAreaOnly { get; set; } = true;
    public bool HideCursor { get; set; } = true;
}
