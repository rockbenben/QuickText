using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// Draws placeholder capsules BEHIND a TextBox's glyphs. Every rectangle is asked of the
/// TextBox itself (<see cref="TextBox.GetRectFromCharacterIndex(int, bool)"/>), so wrapping,
/// scrolling and RTL bidi reordering are handled by WPF's own layout — this element never
/// measures text.
/// </summary>
public sealed class PlaceholderLayer : FrameworkElement
{
    private TextBox? _editor;
    private IReadOnlyList<TokenSpan> _spans = System.Array.Empty<TokenSpan>();
    private bool _enabled;

    /// <summary>Bind to the TextBox whose text this layer decorates. Call once.</summary>
    public void Attach(TextBox editor)
    {
        _editor = editor;
        IsHitTestVisible = false;   // clicks belong to the TextBox on top
    }

    /// <summary>New spans to draw. <paramref name="enabled"/> false paints nothing —
    /// that's the "placeholders not enabled for this snippet" case.</summary>
    public void Update(IReadOnlyList<TokenSpan> spans, bool enabled)
    {
        _spans = spans;
        _enabled = enabled;
        InvalidateVisual();
    }

    // Frozen once: OnRender runs on every keystroke and scroll tick.
    private static readonly Brush VarFill = Frozen(0x22, 0x3D, 0xC2, 0xA0);
    private static readonly Pen VarPen = FrozenPen(0x66, 0x3D, 0xC2, 0xA0);
    private static readonly Brush AutoFill = Frozen(0x22, 0xF2, 0xB4, 0x57);
    private static readonly Pen AutoPen = FrozenPen(0x66, 0xF2, 0xB4, 0x57);
    private static readonly Brush NestFill = Frozen(0x22, 0xA9, 0x8C, 0xE8);
    private static readonly Pen NestPen = FrozenPen(0x66, 0xA9, 0x8C, 0xE8);
    private static readonly Pen CursorPen = DashedPen(0xCC, 0x3D, 0xC2, 0xA0);
    private static readonly Pen BadPen = FrozenPen(0xFF, 0xF2, 0x77, 0x7A, 1.4);

    /// <summary>A token longer than this isn't walked character by character (that walk is the
    /// only per-character cost here); its capsule is approximated from the first and last glyph.</summary>
    private const int MaxWalk = 200;

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(byte a, byte r, byte g, byte b, double thickness = 1)
    {
        var pen = new Pen(Frozen(a, r, g, b), thickness);
        pen.Freeze();
        return pen;
    }

    private static Pen DashedPen(byte a, byte r, byte g, byte b)
    {
        var pen = new Pen(Frozen(a, r, g, b), 1) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_enabled || _editor is not { } tb || _spans.Count == 0) return;

        if (!VisibleRange(tb, out int from, out int to)) return;

        foreach (var span in _spans)
        {
            if (span.Start + span.Length <= from || span.Start >= to) continue;   // off-screen
            foreach (var rect in RectsFor(tb, span.Start, span.Length))
            {
                if (rect.Width <= 0 || rect.Height <= 0) continue;
                switch (span.Kind)
                {
                    case TokenKind.Variable:
                        Capsule(dc, rect, VarFill, VarPen);
                        break;
                    case TokenKind.Auto:
                        Capsule(dc, rect, AutoFill, AutoPen);
                        break;
                    case TokenKind.Nested:
                        Capsule(dc, rect, NestFill, NestPen);
                        break;
                    case TokenKind.Cursor:
                        Capsule(dc, rect, null, CursorPen);
                        break;
                    case TokenKind.Invalid:
                        Squiggle(dc, rect);
                        break;
                }
            }
        }
    }

    private static void Capsule(DrawingContext dc, Rect r, Brush? fill, Pen pen) =>
        dc.DrawRoundedRectangle(fill, pen, Rect.Inflate(r, 1, 1), 3, 3);

    /// <summary>A red wave under the token — the "this will paste verbatim" warning.</summary>
    private static void Squiggle(DrawingContext dc, Rect r)
    {
        const double step = 3;
        double y = r.Bottom - 0.5;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(r.Left, y), false, false);
            bool up = true;
            for (double x = r.Left + step; x < r.Right; x += step, up = !up)
                ctx.LineTo(new Point(x, up ? y - 2 : y), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, BadPen, geo);
    }

    /// <summary>Character range currently on screen, so off-screen spans cost nothing.</summary>
    private static bool VisibleRange(TextBox tb, out int from, out int to)
    {
        from = to = 0;
        try
        {
            int firstLine = tb.GetFirstVisibleLineIndex();
            int lastLine = tb.GetLastVisibleLineIndex();
            if (firstLine < 0 || lastLine < firstLine) return false;
            from = tb.GetCharacterIndexFromLineIndex(firstLine);
            int lastStart = tb.GetCharacterIndexFromLineIndex(lastLine);
            if (from < 0 || lastStart < 0) return false;
            to = lastStart + Math.Max(0, tb.GetLineLength(lastLine));
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }   // mid-relayout: skip this frame
    }

    /// <summary>
    /// One rect per DISPLAY line the token occupies — a wrapped token gets two capsules, like a
    /// wrapped selection in a browser. Rects come from the TextBox, so RTL runs (where the
    /// trailing edge sits LEFT of the leading edge) come out correct via Rect.Union.
    /// </summary>
    private static IEnumerable<Rect> RectsFor(TextBox tb, int start, int length)
    {
        int end = Math.Min(start + length, tb.Text.Length);
        if (end <= start) yield break;

        int stepCount = end - start;
        // Long token: approximate from the two ends instead of walking every character.
        if (stepCount > MaxWalk)
        {
            var a = CharBox(tb, start);
            var b = CharBox(tb, end - 1);
            if (a.HasValue && b.HasValue && Math.Abs(a.Value.Y - b.Value.Y) < 0.5)
                yield return Rect.Union(a.Value, b.Value);
            else if (a.HasValue) yield return a.Value;
            yield break;
        }

        Rect? run = null;
        for (int i = start; i < end; i++)
        {
            var box = CharBox(tb, i);
            if (box == null) continue;
            if (run is { } r && Math.Abs(r.Y - box.Value.Y) < 0.5) run = Rect.Union(r, box.Value);
            else
            {
                if (run is { } prev) yield return prev;
                run = box.Value;
            }
        }
        if (run is { } last) yield return last;
    }

    /// <summary>The box of one character: the union of its leading and trailing caret rects.
    /// Null when WPF can't place it (empty line, mid-relayout).</summary>
    private static Rect? CharBox(TextBox tb, int index)
    {
        try
        {
            var lead = tb.GetRectFromCharacterIndex(index, false);
            var trail = tb.GetRectFromCharacterIndex(index, true);
            if (double.IsInfinity(lead.X) || double.IsInfinity(trail.X)) return null;
            if (double.IsInfinity(lead.Y) || double.IsInfinity(trail.Y)) return null;
            if (double.IsNaN(lead.X) || double.IsNaN(trail.X)) return null;
            if (double.IsNaN(lead.Y) || double.IsNaN(trail.Y)) return null;
            return Rect.Union(lead, trail);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
