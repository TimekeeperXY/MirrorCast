namespace MirrorCast.Models;

public class AppConfig
{
    public string? LastProcessName { get; set; }
    public string? LastWindowTitle { get; set; }
    public string? LastMonitorDeviceName { get; set; }
    public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
    public bool ClientAreaOnly { get; set; } = true;
    public bool HideCursor { get; set; } = true;
    public bool ShowSyntheticCursor { get; set; } = true;
    public bool StartWithWindows { get; set; }

    /// <summary>Set once the first-run walkthrough has been shown, so it never reappears.</summary>
    public bool HasSeenOnboarding { get; set; }
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+M";
    public string StopHotkey { get; set; } = "Ctrl+Alt+Shift+M";
    public double PresentationZoomFactor { get; set; } = 2.0;
    public int PointerEffectSize { get; set; } = 240;
    public string ScreenZoomHotkey { get; set; } = "Ctrl+Alt+Shift+Z";
    public string MagnifierHotkey { get; set; } = "Ctrl+Alt+Shift+L";
    public string SpotlightHotkey { get; set; } = "Ctrl+Alt+Shift+P";
    public List<RecentWindowEntry> RecentWindows { get; set; } = new();
}

public class RecentWindowEntry
{
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
