using System;
using System.Windows;
using System.Windows.Interop;
using QuickText.App.Interop;

namespace QuickText.App.Ui;

internal static class WindowTheming
{
    /// <summary>Mirror the window for a right-to-left UI language (Arabic). WPF flips standard
    /// layout automatically; call once at construction, so a language change takes effect on
    /// windows opened afterwards.</summary>
    public static void ApplyFlowDirection(Window w) =>
        w.FlowDirection = Core.Localization.LocalizationService.Instance.Culture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Match the native title bar to the app's own theme. Named for what it did when there
    /// was only one theme; it now follows <see cref="ThemeService.IsLight"/>, because a dark title
    /// bar over light content looks like a half-repainted window.</summary>
    public static void UseDarkChrome(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = ThemeService.IsLight ? 0 : 1;
            try
            {
                NativeMethods.DwmSetWindowAttribute(
                    hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            }
            catch { /* older OS without the attribute — harmless */ }
            // Cloak until WPF has presented its first frame. Between ShowWindow and that first
            // present there is a real gap. Measured by sampling screen pixels while a window opened:
            // the window's first DWM-composed frame arrives ~50ms BEFORE the content and is plain
            // white, i.e. a white flash on every dark-theme open. A WM_ERASEBKGND hook was tried
            // first and disproven — the white is the surface's initial composition, before any
            // erase cycle.
            // Cloaking (the flag DWM itself uses for windows on inactive virtual desktops) keeps
            // the window out of composition until the content frame exists, and costs no perceived
            // latency: the measured white phase is replaced by the same duration of nothing.
            Cloak(hwnd, true);
            // Backstop. ContentRendered below is the normal reveal, but WPF does not promise it: it
            // never fires for a window with no content, and it is a low-priority dispatcher callback
            // that a window closed or hidden before its first render can miss. With only that one
            // path, a miss leaves a window that is invisible on screen yet holds focus and sits in
            // alt-tab — and for the ShowDialog() windows (AppDialog, VariablesDialog, BodyEditor)
            // that is an invisible MODAL: the owner stops responding and there is nothing to click.
            // A flash is a blemish; an unclosable window is a lost session, so the reveal gets a
            // timer that fires regardless. Uncloaking twice is a no-op.
            var reveal = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Loaded, w.Dispatcher)
                { Interval = TimeSpan.FromMilliseconds(600) };
            reveal.Tick += (_, _) => { reveal.Stop(); Cloak(new WindowInteropHelper(w).Handle, false); };
            reveal.Start();
        };
        // First frame is committed; let DWM show the window.
        w.ContentRendered += (_, _) => Cloak(new WindowInteropHelper(w).Handle, false);
    }

    /// <summary>Hide or reveal a window at the DWM level. Note this cannot report failure: the API
    /// returns an HRESULT rather than throwing, so a refused uncloak is invisible here — which is
    /// exactly why the reveal has two independent triggers rather than one.</summary>
    private static void Cloak(IntPtr hwnd, bool on)
    {
        if (hwnd == IntPtr.Zero) return;
        int v = on ? 1 : 0;
        try { NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAK, ref v, sizeof(int)); }
        catch { /* pre-Win8 dwmapi without the attribute — falls back to the flash, not a failure */ }
    }

    /// <summary>Breathing room left below a capped dialog so it does not sit flush against the
    /// taskbar edge.</summary>
    private const double HeightCapMargin = 72;

    /// <summary>Cap a <c>SizeToContent="Height"</c> dialog to the work area of the monitor it opens
    /// on, WITHOUT touching where it opens (unlike <see cref="PlaceOnActiveMonitor"/>, which also
    /// repositions — wrong for a CenterOwner modal).
    /// <para>A window whose height is decided entirely by its content has no upper bound. Enough
    /// <c>{variables}</c> in one snippet, or a long enough message, and it grows past the bottom of
    /// the screen — and because these dialogs are <c>ResizeMode="NoResize"</c>, what goes off the
    /// bottom is the OK button, with no way to resize or scroll down to it. The user cannot finish
    /// the action at all, only Esc out of it.</para>
    /// <para>The cap cannot be a constant: the same number has to serve a 1366x768 laptop (about
    /// 614 DIP of work area) and a 4K display, and it is wrong at one end or the other.</para>
    /// <para>Measured against the OWNER's monitor rather than this window's. At SourceInitialized a
    /// CenterOwner / CenterScreen dialog has not been positioned yet, so asking which monitor IT is
    /// on describes where it started, not where it will land.</para>
    /// <para>Pair this with a ScrollViewer around the part that grows. Capping alone only moves the
    /// clip — the buttons still disappear, just at a different height.</para></summary>
    public static void CapHeightToMonitor(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            if (App.InSmoke) return;   // --shots sets its own simulated work-area cap after Show
            double cap = OwnerOrCursorWorkArea(w).Height - HeightCapMargin;
            w.MaxHeight = Math.Max(cap, w.MinHeight);
        };
    }

    /// <summary>Work area (DIPs) of the monitor the owner window is on; the cursor's monitor when
    /// there is no owner (VariablesDialog is summoned from the panel and can be ownerless, and the
    /// parameterless BodyEditorWindow ctor used by --smoke has none either).
    /// <para>"Where will this child window land?" — the question every owned window has to answer
    /// before it can size or cap itself, and it cannot ask its OWN handle: at SourceInitialized a
    /// CenterOwner / CenterScreen window has not been positioned yet.</para></summary>
    internal static Rect OwnerOrCursorWorkArea(Window w)
    {
        if (w.Owner is { } owner)
        {
            var oh = new WindowInteropHelper(owner).Handle;
            if (oh != IntPtr.Zero)
                return MonitorWorkAreaDip(
                    NativeMethods.MonitorFromWindow(oh, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        return CursorWorkArea();
    }

    /// <summary>
    /// Place a dialog (Settings / Manager) the same way the search panel positions itself: on the
    /// monitor the user is actually on — the one under the cursor, where they clicked to open it —
    /// centered, capped to that monitor's work area, and nudged fully on-screen so nothing (the
    /// Save button, the editor) ends up under the taskbar or off a short / secondary display.
    /// One shared implementation so each window doesn't reinvent (or forget) it.
    /// </summary>
    public static void PlaceOnActiveMonitor(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        Rect wa = default;
        bool settled = false;
        void Place()
        {
            if (settled || App.InSmoke) return;   // --smoke parks windows off-screen and never shows them
            // Top-centered: horizontal centre needs only the width (a set Width, or ActualWidth once
            // laid out), and the vertical anchor is a fixed offset — so this can run in
            // SourceInitialized, BEFORE the first paint and before SizeToContent finalizes. Centering
            // vertically would need the height, which isn't known that early, forcing a post-paint
            // reposition that flashes. If the (auto-)height then overflows the work area, pull up.
            double width = w.ActualWidth > 0 ? w.ActualWidth : w.Width;
            double height = w.ActualHeight;
            double left = wa.Left + Math.Max(0, (wa.Width - width) / 2);
            double top = wa.Top + wa.Height * 0.08;
            if (height > 0 && top + height > wa.Bottom) top = Math.Max(wa.Top, wa.Bottom - height);
            w.Left = Math.Max(wa.Left, Math.Min(left, wa.Right - width));
            w.Top = top;
        }
        w.SourceInitialized += (_, _) =>
        {
            wa = CursorWorkArea();
            // Cap HEIGHT only — a too-tall window pushes the Save button under the taskbar (the
            // card area scrolls instead). NOT width: clamping a fixed-width / MinWidth window below
            // its design width would clip content (there's no horizontal scroll), so a slightly-too-
            // wide window that the Place() clamp keeps left-aligned on-screen is the lesser evil.
            w.MaxHeight = wa.Height;
            Place();   // position pre-paint so there's no origin→final flash
        };
        // Re-assert once the real size is known (pulls a very tall window up to fit); stop after the
        // window is shown so a later user resize (Manager is resizable) isn't yanked back.
        w.Loaded += (_, _) => Place();
        w.SizeChanged += (_, _) => Place();
        w.ContentRendered += (_, _) => { Place(); settled = true; };
    }

    /// <summary>Per-MONITOR device-px → DIP factor (96 / that monitor's effective DPI), via
    /// Shcore's <c>GetDpiForMonitor</c>. The app is PerMonitorV2-DPI-aware (see app.manifest), so
    /// there is no longer a single scale for the whole virtual desktop — every monitor-geometry
    /// conversion must ask for ITS OWN monitor's DPI. Falls back to 1.0 (no scaling) if the handle
    /// is null or the API fails — GetDpiForMonitor has existed since Windows 8.1, well below this
    /// app's minimum OS, so that path is not expected to be hit in practice.</summary>
    internal static double PxToDipFactor(IntPtr monitor)
    {
        try
        {
            if (monitor != IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
                return 96.0 / dpiX;
        }
        catch { /* Shcore unavailable — fall through */ }
        return 1.0;
    }

    /// <summary>Work area (DIPs) of the given monitor, converted with THAT monitor's own DPI; the
    /// primary work area if the handle is null or unavailable. The one place the monitor-rcWork→DIP
    /// conversion lives (dialogs + panel).</summary>
    internal static Rect MonitorWorkAreaDip(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero)
        {
            var mi = new NativeMethods.MONITORINFO
                { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                double k = PxToDipFactor(monitor);
                return new Rect(mi.rcWork.Left * k, mi.rcWork.Top * k,
                    (mi.rcWork.Right - mi.rcWork.Left) * k, (mi.rcWork.Bottom - mi.rcWork.Top) * k);
            }
        }
        return SystemParameters.WorkArea;
    }

    /// <summary>
    /// Work area (DIPs) of the monitor containing the given DIP point; the primary's as a fallback.
    /// <para>This is what a REMEMBERED window position must be clamped against.
    /// <see cref="SystemParameters.WorkArea"/> describes the PRIMARY monitor only, so clamping a
    /// remembered position to it drags any window that was left on another monitor back onto the
    /// primary — and for a monitor sitting to the LEFT of the primary (negative X, which Windows
    /// allows) the clamp pins it to x=0, i.e. hard against the primary's left edge. That is the
    /// "it always opens in the top-left corner" report.</para>
    /// <para>Under per-monitor DPI awareness there is no single px↔DIP factor to invert a DIP point
    /// back to a px point for <c>MonitorFromPoint</c> — each monitor can have its own scale. Instead
    /// this walks every monitor via <c>EnumDisplayMonitors</c>, converts each one's work area to DIP
    /// with ITS OWN DPI, and returns the first whose DIP rect contains the point. If none does (the
    /// point is just off every monitor's edge), it falls back to the DIP rect whose centre is
    /// nearest — the same "nearest monitor" behaviour <c>MONITOR_DEFAULTTONEAREST</c> gives.</para>
    /// </summary>
    internal static Rect MonitorWorkAreaDipAt(double xDip, double yDip)
    {
        try
        {
            var point = new Point(xDip, yDip);
            Rect? containing = null;
            Rect? nearest = null;
            double nearestDistSq = double.MaxValue;

            bool Callback(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
            {
                var wa = MonitorWorkAreaDip(hMonitor);
                if (wa.Contains(point)) { containing = wa; return false; }   // stop early — found it
                double cx = wa.Left + wa.Width / 2, cy = wa.Top + wa.Height / 2;
                double distSq = (cx - xDip) * (cx - xDip) + (cy - yDip) * (cy - yDip);
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = wa; }
                return true;
            }

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
            if (containing is { } c) return c;
            if (nearest is { } n) return n;
        }
        catch { /* fall through to the primary work area */ }
        return SystemParameters.WorkArea;
    }

    /// <summary>Work area (DIPs) of the monitor under the mouse cursor; primary as a fallback.</summary>
    private static Rect CursorWorkArea()
    {
        try
        {
            if (NativeMethods.GetCursorPos(out var pt))
                return MonitorWorkAreaDip(NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST));
        }
        catch { /* fall through to the primary work area */ }
        return SystemParameters.WorkArea;
    }
}
