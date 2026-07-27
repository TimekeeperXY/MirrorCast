using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MirrorCast.Interop;

namespace MirrorCast.Services;

public class HotkeyService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0x0B00;

    public void Initialize(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public bool Register(string hotkeyText, Action handler)
    {
        if (!TryParse(hotkeyText, out uint modifiers, out uint vk)) return false;

        int id = _nextId++;
        if (!User32.RegisterHotKey(_hwnd, id, modifiers, vk)) return false;

        _handlers[id] = handler;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
            User32.UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// True when the combo can actually be claimed right now. Registers it briefly and
    /// releases it again, so the caller can warn about clashes with other apps
    /// (RegisterHotKey fails outright if something else already owns the combo).
    /// </summary>
    public bool IsAvailable(string hotkeyText)
    {
        if (!TryParse(hotkeyText, out uint modifiers, out uint vk)) return false;

        int probeId = 0x0BFF;
        if (!User32.RegisterHotKey(_hwnd, probeId, modifiers, vk)) return false;

        User32.UnregisterHotKey(_hwnd, probeId);
        return true;
    }

    /// <summary>Formats a WPF key + modifier combo the way <see cref="TryParse"/> reads it.</summary>
    public static string? Format(ModifierKeys modifiers, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None)
        {
            return null; // modifier on its own is not a shortcut
        }

        // A bare key would swallow normal typing everywhere, so require a modifier.
        if (modifiers == ModifierKeys.None) return null;

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        parts.Add(key switch
        {
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "NumPad" + (key - Key.NumPad0),
            _ => key.ToString()
        });

        return string.Join("+", parts);
    }

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= User32.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= User32.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= User32.MOD_SHIFT;
                    break;
                case "WIN":
                    modifiers |= User32.MOD_WIN;
                    break;
                default:
                    if (part.Length == 1)
                        vk = char.ToUpperInvariant(part[0]);
                    else if (Enum.TryParse<System.Windows.Forms.Keys>(part, true, out var key))
                        vk = (uint)key;
                    break;
            }
        }

        return vk != 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
