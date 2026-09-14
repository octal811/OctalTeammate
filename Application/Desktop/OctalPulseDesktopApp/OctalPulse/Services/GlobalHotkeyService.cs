using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace OctalPulse.Services;

/// <summary>
/// Registers and manages global Win32 hotkeys, dispatching them via the HotkeyFired event.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    // Win32 constants
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_NOREPEAT = 0x4000;

    // Hotkey IDs
    public const int HK_SHOW_MAIN = 1;
    public const int HK_MAJOR_TASKS = 2;
    public const int HK_MINOR_TASKS = 3;
    public const int HK_STOPWATCH = 4;
    public const int HK_CALENDAR = 5;
    public const int HK_MEDIA = 6;

    // Virtual key codes
    private const uint VK_W = 0x57;
    private const uint VK_J = 0x4A;
    private const uint VK_N = 0x4E;
    private const uint VK_T = 0x54;
    private const uint VK_C = 0x43;
    private const uint VK_M = 0x4D;

    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private bool _registered;
    private bool _disposed;

    public event Action<int>? HotkeyFired;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Attach to the main window's HwndSource. Call this from SourceInitialized.
    /// </summary>
    public void Attach(HwndSource source)
    {
        _source = source;
        _hwnd = source.Handle;
        source.AddHook(WndProc);
        RegisterAll();
    }

    private void RegisterAll()
    {
        if (_hwnd == IntPtr.Zero || _registered) return;

        RegisterHotKey(_hwnd, HK_SHOW_MAIN,   MOD_ALT | MOD_NOREPEAT, VK_W);
        RegisterHotKey(_hwnd, HK_MAJOR_TASKS,  MOD_ALT | MOD_NOREPEAT, VK_J);
        RegisterHotKey(_hwnd, HK_MINOR_TASKS,  MOD_ALT | MOD_NOREPEAT, VK_N);
        RegisterHotKey(_hwnd, HK_STOPWATCH,    MOD_ALT | MOD_NOREPEAT, VK_T);
        RegisterHotKey(_hwnd, HK_CALENDAR,     MOD_ALT | MOD_NOREPEAT, VK_C);
        RegisterHotKey(_hwnd, HK_MEDIA,        MOD_ALT | MOD_NOREPEAT, VK_M);

        _registered = true;
    }

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero || !_registered) return;

        UnregisterHotKey(_hwnd, HK_SHOW_MAIN);
        UnregisterHotKey(_hwnd, HK_MAJOR_TASKS);
        UnregisterHotKey(_hwnd, HK_MINOR_TASKS);
        UnregisterHotKey(_hwnd, HK_STOPWATCH);
        UnregisterHotKey(_hwnd, HK_CALENDAR);
        UnregisterHotKey(_hwnd, HK_MEDIA);

        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            HotkeyFired?.Invoke(id);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
