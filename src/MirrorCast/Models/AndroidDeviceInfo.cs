namespace MirrorCast.Models;

public sealed class AndroidDeviceInfo
{
    public string Serial { get; init; } = string.Empty;
    public string Name { get; init; } = "安卓设备";
    public string Transport { get; init; } = "USB";
    public string DisplayName => $"{Name} · {Transport} ({Serial})";
}
