using QuickText.Core.Interop;

namespace QuickText.App.Interop;

public sealed class GlobalHotkey : IDisposable
{
    public const int DefaultId = 0xA11C;

    private readonly IntPtr _hwnd;
    private readonly HotkeyDefinition _def;
    private readonly int _id;   // distinct per registered hotkey (summon vs capture)
    private bool _registered;

    public event Action? Pressed;

    public GlobalHotkey(IntPtr hwnd, HotkeyDefinition def, int id = DefaultId)
    {
        _hwnd = hwnd; _def = def; _id = id;
    }

    public bool TryRegister(out string error)
    {
        error = "";
        _registered = NativeMethods.RegisterHotKey(_hwnd, _id, _def.Modifiers, _def.Vk);
        if (!_registered)
        {
            int win32 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            error = $"RegisterHotKey failed (hotkey may be in use), win32err={win32}";
        }
        return _registered;
    }

    /// <summary>Call from the window's WndProc hook.</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_registered) NativeMethods.UnregisterHotKey(_hwnd, _id);
        _registered = false;
    }
}
