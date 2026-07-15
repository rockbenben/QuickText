using System.Windows;
using static QuickText.App.Interop.NativeMethods;

namespace QuickText.App.Interop;

public static class PasteEngine
{
    // {光标} walks the caret back with synthesized Left presses; past this distance the
    // walk would be an arrow-key storm in the target app, so leave the caret at the end.
    private const int MaxCaretWalk = 200;

    // Cross-send state so a rapid second paste doesn't mistake our own just-pasted snippet for the
    // user's clipboard and "restore" to the snippet. CAPTURE keys off the Win32 clipboard SEQUENCE
    // NUMBER (unchanged since our paste ⇒ what's on the clipboard is still OURS; any user copy,
    // even of identical bytes, bumps it). RESTORE keys off CONTENT (below), so a clipboard manager
    // that bumps the sequence but keeps our content still triggers the restore. UI-thread only.
    private static string? _pendingRestore;   // the user's real clipboard still owed a restore
    private static uint _pastedSeq;            // clipboard sequence number right after our last paste

    /// <summary>The user's real clipboard text to restore later, seeing through our own pending paste.</summary>
    private static string? CaptureOriginal()
    {
        string? original = null;
        try { if (Clipboard.ContainsText()) original = Clipboard.GetText(); } catch { }
        // Clipboard untouched since our own last paste → the text on it is our snippet, not the
        // user's; the real original is the one still owed a restore.
        if (_pendingRestore != null && GetClipboardSequenceNumber() == _pastedSeq)
            original = _pendingRestore;
        return original;
    }

    /// <summary>
    /// After we've put our content on the clipboard, schedule restoring <paramref name="original"/>.
    /// <paramref name="stillOurs"/> is the "is our pasted content still there" check; pass null for
    /// content that can't be compared (images) to fall back to a sequence-number gate — restore only
    /// if nothing has touched the clipboard since our paste. (Content compare is preferred for text
    /// because it also survives a clipboard manager that bumps the sequence but keeps our text.)
    /// </summary>
    private static void ScheduleRestore(string original, Func<bool>? stillOurs, int delayMs)
    {
        _pendingRestore = original;
        uint mySeq = GetClipboardSequenceNumber();
        _pastedSeq = mySeq;
        var restore = original;
        System.Threading.Tasks.Task.Delay(delayMs).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool ours;
                try { ours = stillOurs != null ? stillOurs() : GetClipboardSequenceNumber() == mySeq; }
                catch { ours = false; }
                if (ours) SetClipboardTextWithRetry(restore);
                if (_pastedSeq == mySeq) _pendingRestore = null;   // resolve our entry unless superseded
            });
        });
    }

    public static void SendBackspaces(int count)
    {
        if (count <= 0) return;
        var inputs = new INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = Key(VK_BACK, false);
            inputs[i * 2 + 1] = Key(VK_BACK, true);
        }
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    /// <summary>Returns true when the Ctrl+V was actually dispatched (callers may arm undo on it).</summary>
    public static bool Paste(string text, bool restoreClipboard, bool autoSend = false, int caretFromEnd = 0)
    {
        if (string.IsNullOrEmpty(text)) return false; // nothing to paste — do NOT fire Ctrl+V on stale clipboard

        string? original = restoreClipboard ? CaptureOriginal() : null;

        if (!SetClipboardTextWithRetry(text))
            return false; // clipboard set failed — do NOT paste whatever was there before

        // Ctrl down, V down, V up, Ctrl up
        var inputs = new[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true),
        };
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());

        if (caretFromEnd > 0 && caretFromEnd <= MaxCaretWalk)
        {
            // {光标}: after the paste lands, walk the caret back to the marker position.
            System.Threading.Tasks.Task.Delay(90).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var moves = new INPUT[caretFromEnd * 2];
                    for (int i = 0; i < caretFromEnd; i++)
                    {
                        moves[i * 2] = Key(VK_LEFT, false);
                        moves[i * 2 + 1] = Key(VK_LEFT, true);
                    }
                    SendInput((uint)moves.Length, moves, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
                });
            });
        }
        // Callers suppress autoSend when the body has a {光标} token (even a trailing one,
        // where caretFromEnd is 0); this guard only covers the mid-body case redundantly.
        else if (autoSend && caretFromEnd == 0)
        {
            // Let the paste land first, then press Enter to send in the target app.
            System.Threading.Tasks.Task.Delay(90).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var enter = new[] { Key(VK_RETURN, false), Key(VK_RETURN, true) };
                    SendInput((uint)enter.Length, enter, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
                });
            });
        }

        if (restoreClipboard && original != null)
            // Slow apps read the clipboard well after Ctrl+V lands, and the bigger the text the
            // later that read tends to happen — restoring too early pastes the OLD content.
            ScheduleRestore(original, () => Clipboard.ContainsText() && Clipboard.GetText() == text,
                Math.Min(250 + text.Length / 100, 3000));
        return true;
    }

    /// <summary>
    /// Type short text as KEYEVENTF_UNICODE key events — no clipboard involved.
    /// Used by expansion-undo to retype the abbreviation the user originally typed.
    /// </summary>
    public static void SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var inputs = new INPUT[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeKey(text[i], false);
            inputs[i * 2 + 1] = UnicodeKey(text[i], true);
        }
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static INPUT UnicodeKey(char c, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0) }
        }
    };

    /// <summary>Copy-only (no paste): leave text on the clipboard for the user to paste manually.</summary>
    public static void CopyText(string text)
    {
        if (!string.IsNullOrEmpty(text)) SetClipboardTextWithRetry(text);
    }

    public static void CopyImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        if (image != null) { try { Clipboard.SetImage(image); } catch { } }
    }

    public static void PasteImage(System.Windows.Media.Imaging.BitmapSource image, bool autoSend = false,
        bool restoreClipboard = false)
    {
        if (image == null) return;

        // Same courtesy the text path extends: don't destroy the user's copied text. Uses the
        // shared capture so a text-expansion-then-image-expansion within the restore window still
        // preserves the user's real clipboard (not the intervening text snippet).
        string? original = restoreClipboard ? CaptureOriginal() : null;

        try { Clipboard.SetImage(image); } catch { return; }

        var inputs = new[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true),
        };
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());

        if (autoSend)
        {
            System.Threading.Tasks.Task.Delay(90).ContinueWith(_ =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var enter = new[] { Key(VK_RETURN, false), Key(VK_RETURN, true) };
                    SendInput((uint)enter.Length, enter, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
                });
            });
        }

        if (restoreClipboard && original != null)
            // Images can't be content-compared, so gate on the sequence number: restore only if the
            // clipboard is untouched since our SetImage. A "still any image" check would clobber a
            // screenshot the user copied in the meantime, or revert a rapid second image send.
            ScheduleRestore(original, null, 500);
    }

    private static bool SetClipboardTextWithRetry(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetText(text); return true; }
            catch { System.Threading.Thread.Sleep(30); }
        }
        return false;
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } }
    };
}
