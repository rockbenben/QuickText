using System.Runtime.InteropServices;
using QuickText.Core.Interop;

namespace QuickText.App.Interop;

/// <summary>
/// A low-level keyboard hook that fires <paramref name="onTap"/> when the configured modifier is
/// tapped (or double-tapped) on its own — the summon trigger that <c>RegisterHotKey</c> can't do.
/// Separate from <see cref="KeyboardHook"/> (which is only installed for abbreviations) so it can
/// run independently of the abbreviation setting. The callback runs on the installing (UI) thread.
/// </summary>
public sealed class ModifierTapHook : IDisposable
{
    private readonly ModifierTapDetector _detector;
    private readonly Action _onTap;
    private readonly NativeMethods.HookProc _proc;   // kept alive to avoid GC of the delegate
    private IntPtr _hook = IntPtr.Zero;

    public ModifierTapHook(uint targetVk, bool requireDouble, Action onTap)
    {
        _detector = new ModifierTapDetector(targetVk, requireDouble);
        _onTap = onTap;
        _proc = Callback;
    }

    public void Install()
    {
        if (_hook == IntPtr.Zero)
            _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool down = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool up = msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP;
            if (down || up)
            {
                // An exception must never cross the native callback boundary (Windows would
                // forcibly unhook). Never swallow the key — the tap is only a side effect.
                try
                {
                    var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                    if (_detector.Feed(data.vkCode, down)) _onTap();
                }
                catch { }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
