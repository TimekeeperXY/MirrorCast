namespace MirrorCast.Models;

public sealed class AndroidMirrorOptions
{
    public string? Serial { get; init; }
    public string? Address { get; init; }
    public ushort Port { get; init; } = 5555;
    public bool Control { get; init; } = true;
    public bool Audio { get; init; } = true;
    public bool TurnScreenOff { get; init; }
    public int MaxFps { get; init; } = 60;
}
