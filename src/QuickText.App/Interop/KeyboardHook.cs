using System.Text;
using QuickText.Core.Abbr;
using static QuickText.App.Interop.NativeMethods;

namespace QuickText.App.Interop;

public sealed class KeyboardHook : IDisposable
{
    private readonly AbbrMatcher _matcher;
    private readonly Func<bool> _isEnabled;
    private readonly Func<bool> _restoreClipboard;
    private readonly Action<string>? _recordUse;   // record a snippet use (for frecency), by id
    private readonly HashSet<char> _terminators;
    private readonly HookProc _proc;        // keep alive to avoid GC
    private readonly HookProc _mouseProc;   // ditto
    private IntPtr _hook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private volatile bool _suppressingSelfInput;

    private readonly HashSet<string> _blacklist;   // lowercase process names, no ".exe"
    private uint _lastFgPid;
    private bool _lastFgBlocked;

    // One-shot expansion undo: pressing Backspace as the very next key reverts the
    // expansion back to the typed abbreviation. Hook callback and dispatcher actions
    // both run on the UI thread, so no synchronization is needed.
    private (IntPtr Target, int Backspaces, string Abbr, DateTime Armed)? _pendingUndo;

    public KeyboardHook(AbbrMatcher matcher, Func<bool> isEnabled, string terminatorChars,
        Func<bool> restoreClipboard, string blacklist = "", Action<string>? recordUse = null)
    {
        _matcher = matcher;
        _isEnabled = isEnabled;
        _restoreClipboard = restoreClipboard;
        _terminators = new HashSet<char>(terminatorChars);
        _blacklist = ParseBlacklist(blacklist);
        _recordUse = recordUse;
        _proc = HookCallback;
        _mouseProc = MouseCallback;
    }

    private static HashSet<string> ParseBlacklist(string raw)
    {
        var set = new HashSet<string>();
        foreach (var part in (raw ?? "").Split(';', ',', '\n'))
        {
            var name = part.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            if (name.Length > 0) set.Add(name.ToLowerInvariant());
        }
        return set;
    }

    /// <summary>Cached per foreground-PID: is expansion disabled for this app?</summary>
    private bool IsForegroundBlocked(uint pid)
    {
        if (_blacklist.Count == 0) return false;
        if (pid == _lastFgPid) return _lastFgBlocked;
        _lastFgPid = pid;
        try
        {
            var name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
            _lastFgBlocked = _blacklist.Contains(name.ToLowerInvariant());
        }
        catch { _lastFgBlocked = false; }
        if (_lastFgBlocked) _matcher.Reset();
        return _lastFgBlocked;
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
        // Clicks move the caret to a place the typed buffer knows nothing about — treat
        // any button-down as a token break (and cancel the one-shot undo offer).
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);
    }

    public void Uninstall()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
            {
                try { _matcher.Reset(); _pendingUndo = null; } catch { }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || _suppressingSelfInput || !_isEnabled())
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        try
        {
            // Only keydowns matter; bail before any foreground-window syscalls so the
            // keyup half of every keystroke system-wide costs nothing.
            int msg = wParam.ToInt32();
            if (msg != WM_KEYDOWN && msg != WM_SYSKEYDOWN)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            // No triggers at all (and no undo armed) → nothing this hook could ever do.
            if (_matcher.IsEmpty && _pendingUndo == null)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            // Skip abbreviation handling when the user is typing in QuickText's own windows
            // (search box / manager editor) — expanding there would corrupt what they type.
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fg, out uint fgPid);
                if (fgPid == (uint)Environment.ProcessId)
                    return CallNextHookEx(_hook, nCode, wParam, lParam);
                if (IsForegroundBlocked(fgPid))
                    return CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            uint vk = data.vkCode;

            // Shift / CapsLock change no text: they must neither break the token being
            // typed (";Nh" needs Shift mid-abbr) nor consume the one-shot undo offer.
            if (vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT || vk == VK_CAPITAL)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            // Expansion undo: only the very next keydown counts, and only when the user
            // is still in the same window shortly after the expansion (a later Backspace
            // in another context must never trigger a multi-character delete).
            if (_pendingUndo is { } undo)
            {
                _pendingUndo = null;   // one-shot: any key consumes the offer
                if (vk == VK_BACK
                    && fg == undo.Target
                    && DateTime.UtcNow - undo.Armed < TimeSpan.FromSeconds(5))
                {
                    DispatchUndo(undo.Backspaces, undo.Abbr);
                    return (IntPtr)1;   // swallow the Backspace — the undo does the deleting
                }
            }

            if (vk == VK_BACK)
            {
                _matcher.Backspace();   // typo correction keeps the rest of the abbr alive
                return CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            char? ch = VkToChar(vk, data.scanCode, fg);

            if (ch is char c)
            {
                if (_terminators.Contains(c))
                {
                    var match = _matcher.OnTerminator();
                    if (match != null)
                    {
                        DispatchExpansion(match);
                        return (IntPtr)1; // swallow the terminator
                    }
                }
                else
                {
                    _matcher.FeedChar(c);
                }
            }
            else
            {
                _matcher.Reset(); // navigation / control keys break the token
            }
        }
        catch
        {
            // An exception must never cross the native callback boundary (it would crash
            // the process and Windows would forcibly unhook). Reset and fall through.
            try { _matcher.Reset(); } catch { }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void DispatchExpansion(AbbrMatch match)
    {
        // Run off the hook thread so we can prompt / synthesize input without re-entrancy.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var target = GetForegroundWindow();   // the app where the abbreviation was typed
            var sn = match.Snippet;

            // Image snippet: delete the typed abbr, then paste (or copy) the image. No undo —
            // "N backspaces" has no reliable meaning for a pasted image across apps.
            if (sn.IsImage)
            {
                var image = QuickText.App.Ui.ImageLoader.Load(sn.ImagePath);
                if (image == null) return;   // file missing — leave the typed abbreviation alone
                _suppressingSelfInput = true;
                try
                {
                    PasteEngine.SendBackspaces(match.BackspaceCount);
                    if (sn.OutputMode == "copy") PasteEngine.CopyImage(image);
                    else PasteEngine.PasteImage(image, autoSend: sn.OutputMode == "paste-enter",
                        restoreClipboard: _restoreClipboard());
                    _pendingUndo = null;
                    if (!string.IsNullOrEmpty(sn.Id)) _recordUse?.Invoke(sn.Id);
                }
                finally { _suppressingSelfInput = false; }
                return;
            }

            // Shared resolver: per-snippet opt-in gate, {变量} prompt, clipboard/date/cursor
            // tokens — identical to the search panel's send and copy paths.
            var resolved = QuickText.App.Ui.BodyResolver.Resolve(sn.Body, sn.UseVariables);
            if (resolved == null) return;          // cancelled — leave the typed abbreviation untouched
            if (resolved.Prompted) ForceForeground(target);   // the modal dialog stole focus; hand it back
            string text = resolved.Text;
            int caret = resolved.CaretFromEnd;
            bool prompted = resolved.Prompted;
            // Per-snippet output override. "copy": remove the typed abbr, put the text on the
            // clipboard, paste nothing. "paste-enter": auto-Enter after the paste (unless a
            // {光标} token says "keep typing here"). Global AutoSend never applies to abbrs.
            bool copyOnly = sn.OutputMode == "copy";
            bool autoSend = sn.OutputMode == "paste-enter" && !resolved.HasCursor;

            void Emit()
            {
                _suppressingSelfInput = true;
                try
                {
                    PasteEngine.SendBackspaces(match.BackspaceCount);
                    if (copyOnly)
                    {
                        PasteEngine.CopyText(text);
                        _pendingUndo = null;   // nothing was pasted; Backspace stays literal
                        if (!string.IsNullOrEmpty(sn.Id)) _recordUse?.Invoke(sn.Id);
                        return;
                    }
                    bool pasted = PasteEngine.Paste(text, restoreClipboard: _restoreClipboard(), autoSend: autoSend, caretFromEnd: caret);
                    // Arm the one-shot undo — except when {光标} moved the caret off the end,
                    // where "delete N from the end" would hit the wrong span. Also skip large
                    // bodies: undoing would synthesize thousands of Backspace events into the
                    // target app (a multi-second delete storm), so there Backspace stays literal.
                    _pendingUndo = null;
                    if (pasted && caret == 0 && !autoSend)   // after auto-Enter the text is submitted — too late to undo
                    {
                        int graphemes = GraphemeCountAtMost(text, MaxUndoGraphemes);
                        if (graphemes <= MaxUndoGraphemes)
                            _pendingUndo = (GetForegroundWindow(), graphemes, match.Abbr, DateTime.UtcNow);
                    }
                    // Count an expansion as a use, so abbreviation-driven snippets also rise
                    // in "最近常用" / frecency ranking (the panel already records its sends).
                    if (pasted && !string.IsNullOrEmpty(sn.Id)) _recordUse?.Invoke(sn.Id);
                }
                finally { _suppressingSelfInput = false; }
            }

            // Only defer when a prompt stole focus — let it settle back on the target first.
            // The common (no-variable) path stays inline, exactly as snappy as before.
            if (prompted)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(Emit), System.Windows.Threading.DispatcherPriority.Background);
            else
                Emit();
        }));
    }

    private void DispatchUndo(int backspaces, string abbr)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _suppressingSelfInput = true;
            try
            {
                PasteEngine.SendBackspaces(backspaces);
                PasteEngine.SendUnicodeText(abbr);
            }
            finally { _suppressingSelfInput = false; }
        }));
    }

    // Undo works by sending one Backspace per grapheme; past this size it's a delete storm.
    private const int MaxUndoGraphemes = 200;

    // Backspace deletes one grapheme (emoji, CRLF pair) per press in most editors, so count
    // text elements rather than UTF-16 chars to avoid over-deleting. Bounded: callers only
    // need the exact count when it's ≤ max, so stop enumerating once past it instead of
    // scanning a multi-hundred-KB body on the UI thread right after the paste.
    private static int GraphemeCountAtMost(string s, int max)
    {
        if (s.Length <= max) return new System.Globalization.StringInfo(s).LengthInTextElements;
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        int n = 0;
        while (e.MoveNext()) if (++n > max) break;
        return n;
    }

    // Reused buffers: this runs for every keystroke system-wide, and the low-level
    // hook callback is single-threaded (the installing thread's message loop), so
    // static reuse is safe and avoids a 256-byte + StringBuilder allocation per key.
    private static readonly byte[] KeyStateBuffer = new byte[256];
    private static readonly StringBuilder CharBuffer = new(8);

    private static char? VkToChar(uint vk, uint scan, IntPtr fg)
    {
        var keyState = KeyStateBuffer;
        GetKeyboardState(keyState);

        // Resolve the layout of the window the user is actually typing into — not
        // QuickText's own UI thread, which may use a different layout (multi-layout users).
        IntPtr layout = IntPtr.Zero;
        if (fg != IntPtr.Zero)
        {
            uint tid = GetWindowThreadProcessId(fg, out _);
            layout = GetKeyboardLayout(tid);
        }

        var sb = CharBuffer;
        sb.Clear();
        int rc = ToUnicodeEx(vk, scan, keyState, sb, sb.Capacity, 0, layout);
        if (rc < 0)
        {
            // Dead key: call again to flush the dead-key state ToUnicodeEx just set into
            // the layout, so we don't corrupt the user's next real keypress. No char yet.
            ToUnicodeEx(vk, scan, keyState, sb, sb.Capacity, 0, layout);
            return null;
        }
        if (rc == 1 && sb.Length == 1)
        {
            char c = sb[0];
            return char.IsControl(c) ? (c == '\r' || c == '\t' ? c : (char?)null) : c;
        }
        return null;
    }

    public void Dispose() => Uninstall();
}
