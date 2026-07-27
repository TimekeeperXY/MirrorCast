using System.Windows;
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
