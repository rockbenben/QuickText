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
    private static DataObject? _pendingRestore;   // the user's real clipboard still owed a restore
    private static uint _pastedSeq;               // clipboard sequence number right after our last paste

    /// <summary>A materialized snapshot of the user's clipboard, seeing through our own pending
    /// paste. Materialized eagerly (text/image/files/HTML/RTF) because IDataObject from
    /// Clipboard.GetDataObject is lazy: the source app may be gone by the time we restore.</summary>
    private static DataObject? CaptureOriginal()
    {
        if (_pendingRestore != null && GetClipboardSequenceNumber() == _pastedSeq)
            return _pendingRestore;

        var copy = new DataObject();
        bool any = false;
        try
        {
            if (Clipboard.ContainsText())
            {
                copy.SetText(Clipboard.GetText());
                any = true;
            }
        }
        catch { }
        try
        {
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                if (image.CanFreeze) image.Freeze();
                copy.SetImage(image);
                any = true;
            }
        }
        catch { }
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                copy.SetFileDropList(Clipboard.GetFileDropList());
                any = true;
            }
        }
        catch { }
        try
        {
            if (Clipboard.ContainsData(DataFormats.Html) && Clipboard.GetData(DataFormats.Html) is string html)
            {
                copy.SetData(DataFormats.Html, html);
                any = true;
            }
        }
        catch { }
        try
        {
            if (Clipboard.ContainsData(DataFormats.Rtf) && Clipboard.GetData(DataFormats.Rtf) is string rtf)
            {
                copy.SetData(DataFormats.Rtf, rtf);
                any = true;
            }
        }
        catch { }

        // Custom/simple formats (CSV, XML spreadsheet, plain byte payloads). Materialize them now
        // too, but only values that can safely be held and written back; complex COM formats are
        // not cloned and stay source-app dependent.
        try
        {
            var source = Clipboard.GetDataObject();
            if (source != null)
            {
                foreach (var format in source.GetFormats())
                {
                    if (copy.GetDataPresent(format)) continue;
                    try
                    {
                        var value = CloneSimpleFormat(source.GetData(format));
                        if (value == null) continue;
                        copy.SetData(format, value);
                        any = true;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return any ? copy : null;
    }

    private static object? CloneSimpleFormat(object? value) => value switch
    {
        string s => s,
        string[] a => (string[])a.Clone(),
        System.Collections.Specialized.StringCollection c => CloneStringCollection(c),
        byte[] b => (byte[])b.Clone(),
        System.IO.MemoryStream ms => new System.IO.MemoryStream(ms.ToArray()),
        System.IO.Stream s => CopyStream(s),
        System.Windows.Media.Imaging.BitmapSource bmp => FreezeBitmap(bmp),
        _ => null,
    };

    private static System.Collections.Specialized.StringCollection CloneStringCollection(
        System.Collections.Specialized.StringCollection source)
    {
        var copy = new System.Collections.Specialized.StringCollection();
        foreach (var item in source) copy.Add(item);
        return copy;
    }

    private static System.IO.MemoryStream CopyStream(System.IO.Stream source)
    {
        if (source.CanSeek) source.Position = 0;
        var copy = new System.IO.MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    private static System.Windows.Media.Imaging.BitmapSource FreezeBitmap(
        System.Windows.Media.Imaging.BitmapSource source)
    {
        if (source.CanFreeze && !source.IsFrozen) source.Freeze();
        return source;
    }

    /// <summary>
    /// How long to leave our content on the clipboard before restoring the user's.
    /// Ctrl+V is only a keystroke: the target app reads the clipboard whenever it gets round to it —
    /// Chromium/Electron apps (VS Code, WeChat, browsers) round-trip it to another process, a window
    /// just pulled to the foreground is still repainting, and RDP/VM sessions redirect it entirely.
    /// Restoring before that read is what makes a paste silently deliver the OLD clipboard, so the
    /// window is generous. Waiting longer is safe: the restore is gated on our content still being
    /// on the clipboard, so a copy by the user in the meantime cancels it rather than clobbering it.
    /// </summary>
    private static int RestoreDelayMs(int textLength) => Math.Min(1200 + textLength / 10, 5000);

    /// <summary>
    /// After we've put our content on the clipboard, schedule restoring <paramref name="original"/>.
    /// <paramref name="stillOurs"/> is the "is our pasted content still there" check; pass null for
    /// content that can't be compared (images) to fall back to a sequence-number gate — restore only
    /// if nothing has touched the clipboard since our paste. (Content compare is preferred for text
    /// because it also survives a clipboard manager that bumps the sequence but keeps our text.)
    /// </summary>
    private static void ScheduleRestore(DataObject original, Func<bool>? stillOurs, int delayMs)
    {
        _pendingRestore = original;
        uint mySeq = GetClipboardSequenceNumber();
        _pastedSeq = mySeq;
        var restore = original;
        System.Threading.Tasks.Task.Delay(delayMs).ContinueWith(_ =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool ours = false;
                bool known = false;
                try
                {
                    ours = stillOurs != null ? stillOurs() : GetClipboardSequenceNumber() == mySeq;
                    known = true;
                }
                catch { /* clipboard locked/unavailable: do not guess that the user replaced it */ }

                if (!known)
                {
                    // Keep _pendingRestore: the next paste can still put the original back.
                    QuickText.Core.Log.Write("clipboard restore check failed");
                    return;
                }

                bool restored = !ours || SetClipboardDataWithRetry(restore);
                if (!restored) QuickText.Core.Log.Write("clipboard restore failed");
                // Keep the pending original when our content is still there but the restore
                // failed; the next paste can try again instead of losing it.
                if (_pastedSeq == mySeq && (!ours || restored)) _pendingRestore = null;
            });
        });
    }

    /// <summary>True when all synthesized Backspace events were accepted by the OS.</summary>
    public static bool SendBackspaces(int count)
    {
        if (count <= 0) return true;
        var inputs = new INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = Key(VK_BACK, false);
            inputs[i * 2 + 1] = Key(VK_BACK, true);
        }
        return SendInput((uint)inputs.Length, inputs,
            System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()) == (uint)inputs.Length;
    }

    /// <summary>Returns true when the Ctrl+V was actually dispatched (callers may arm undo on it).</summary>
    public static bool Paste(string text, bool restoreClipboard, bool autoSend = false, int caretFromEnd = 0)
    {
        if (string.IsNullOrEmpty(text)) return false; // nothing to paste — do NOT fire Ctrl+V on stale clipboard

        DataObject? original = restoreClipboard ? CaptureOriginal() : null;

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
        if (SendInput((uint)inputs.Length, inputs,
                System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()) != (uint)inputs.Length)
        {
            // UIPI (elevated target) or a blocked input queue. Leave the user's clipboard as it was.
            if (restoreClipboard && original != null) SetClipboardDataWithRetry(original);
            return false;
        }

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
                RestoreDelayMs(text.Length));
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
            ki = new KEYBDINPUT
            {
                wVk = 0, wScan = c,
                dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0),
                dwExtraInfo = SelfInputMarker,
            }
        }
    };

    /// <summary>Copy-only (no paste): leave text on the clipboard for the user to paste manually.</summary>
    public static bool CopyText(string text) =>
        !string.IsNullOrEmpty(text) && SetClipboardTextWithRetry(text);

    public static bool CopyImage(System.Windows.Media.Imaging.BitmapSource image) =>
        image != null && SetClipboardImageWithRetry(image);

    public static bool PasteImage(System.Windows.Media.Imaging.BitmapSource image, bool autoSend = false,
        bool restoreClipboard = false)
    {
        if (image == null) return false;

        // Same courtesy the text path extends: preserve the user's whole clipboard, not just text.
        DataObject? original = restoreClipboard ? CaptureOriginal() : null;

        if (!SetClipboardImageWithRetry(image)) return false;

        var inputs = new[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true),
        };
        if (SendInput((uint)inputs.Length, inputs,
                System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()) != (uint)inputs.Length)
        {
            if (restoreClipboard && original != null) SetClipboardDataWithRetry(original);
            return false;
        }

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
            // Images are bigger and slower to read than text, so the window is no shorter.
            ScheduleRestore(original, null, RestoreDelayMs(0));
        return true;
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

    private static bool SetClipboardImageWithRetry(System.Windows.Media.Imaging.BitmapSource image)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetImage(image); return true; }
            catch { System.Threading.Thread.Sleep(30); }
        }
        return false;
    }

    private static bool SetClipboardDataWithRetry(IDataObject data)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetDataObject(data, true); return true; }
            catch { System.Threading.Thread.Sleep(30); }
        }
        return false;
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = up ? KEYEVENTF_KEYUP : 0,
                dwExtraInfo = SelfInputMarker,
            }
        }
    };
}
