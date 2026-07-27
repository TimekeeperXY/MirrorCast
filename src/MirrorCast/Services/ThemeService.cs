using Microsoft.Win32;

namespace MirrorCast.Services;

/// <summary>
/// Tracks the system's light/dark app theme (HKCU Personalize\AppsUseLightTheme) and
/// raises <see cref="ThemeChanged"/> whenever the user changes it, so the app can
/// re-theme live without a restart.
/// </summary>
public class ThemeService : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    public event Action<bool>? ThemeChanged; // bool = isDarkTheme

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsDarkTheme => !ReadAppsUseLightTheme();

    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(ValueName) is int value ? value != 0 : true;
        }
        catch
        {
            return true;
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            ThemeChanged?.Invoke(IsDarkTheme);
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
