using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using QuickText.App.Interop;

namespace QuickText.App.Tests;

/// <summary>
/// Regression tests verifying that low-level key translation observes characters non-destructively
/// without consuming or clearing pending dead keys (e.g. Czech caron/acute accents) intended for target applications.
/// </summary>
public class KeyboardTranslationTests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayout(string name, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnloadKeyboardLayout(IntPtr layout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

    [Fact]
    public void Observing_Czech_keys_preserves_dead_key_composition_for_the_target()
    {
        Exception? failure = null;
        // No hooks, injected input or layout activation: native composition stays on this
        // isolated thread, separate from the user's input queue and the WPF test host.
        var thread = new Thread(() =>
        {
            IntPtr layout = IntPtr.Zero;
            try
            {
                layout = LoadKeyboardLayout("00000405", 0); // Czech QWERTZ
                Assert.NotEqual(IntPtr.Zero, layout);
                const uint deadScan = 0x0D; // acute/caron key beside Backspace
                uint deadVk = MapVirtualKeyEx(deadScan, 1, layout);
                foreach (var (vk, upper, expected) in new (uint, bool, string)[]
                {
                    (0x53, true, "Š"), (0x43, true, "Č"), (0x52, true, "Ř"),
                    (0x53, false, "š"), (0x43, false, "č"), (0x52, false, "ř"),
                    (0x20, false, "ˇ"), (0x58, false, "ˇx")
                })
                {
                    var deadState = KeyState(shift: true);
                    Assert.Null(KeyboardHook.TranslateKey(deadVk, deadScan, deadState, layout));
                    var target = new StringBuilder(8);
                    Assert.True(NativeMethods.ToUnicodeEx(deadVk, deadScan, deadState,
                        target, target.Capacity, 0, layout) < 0, "Expected the Czech caron dead key.");

                    var letterState = KeyState(upper);
                    uint scan = MapVirtualKeyEx(vk, 0, layout);
                    var observed = KeyboardHook.TranslateKey(vk, scan, letterState, layout);
                    Assert.Equal(expected.Length == 1 ? (char?)expected[0] : null, observed);
                    target.Clear();
                    int count = NativeMethods.ToUnicodeEx(vk, scan, letterState,
                        target, target.Capacity, 0, layout);
                    Assert.Equal(expected.Length, count);
                    Assert.Equal(expected, target.ToString(0, count));
                }

                foreach (var (vk, expected) in new (uint, char)[]
                {
                    (0x53, 's'), (0x20, ' '), (0x09, '\t'), (0x0D, '\r')
                })
                    Assert.Equal(expected, KeyboardHook.TranslateKey(vk,
                        MapVirtualKeyEx(vk, 0, layout), KeyState(false), layout));
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (layout != IntPtr.Zero) UnloadKeyboardLayout(layout);
            }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Keyboard translation test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static byte[] KeyState(bool shift)
    {
        var state = new byte[256];
        if (shift) state[0x10] = state[0xA0] = 0x80;
        return state;
    }
}
