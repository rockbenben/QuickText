using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickText.App.Ui;

/// <summary>Logical line numbers, drawn with the same ask-the-TextBox technique as
/// <see cref="PlaceholderLayer"/>: a wrapped logical line spans several display lines, so the
/// number is drawn at the rect of the line's FIRST character.</summary>
public sealed class LineNumberGutter : FrameworkElement
{
    private TextBox? _editor;

    public void Attach(TextBox editor)
    {
        _editor = editor;
        IsHitTestVisible = false;
    }

    public void Refresh() => InvalidateVisual();

    private static readonly Brush NumBrush = FrozenGray();
    // Literal fallback only — the TextBox itself draws with the "Font.Mono" theme resource, so a
    // hardcoded family here could silently drift from it. Resolved lazily (not at type-init time,
    // when this element isn't in the visual tree yet and TryFindResource would find nothing) and
    // cached, since the resource can't change at runtime.
    private static readonly FontFamily FallbackFont = new("Cascadia Mono, Consolas");
    private Typeface? _face;

    private Typeface Face()
    {
        if (_face is { } f) return f;
        var family = (TryFindResource("Font.Mono") as FontFamily) ?? FallbackFont;
        _face = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return _face;
    }

    private static SolidColorBrush FrozenGray()
    {
        var b = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x7C));
        b.Freeze();
        return b;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_editor is not { } tb || ActualWidth <= 0) return;

        var text = tb.Text;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Find where the visible viewport starts so we don't have to walk (and layout-query)
        // every logical line in the document on every keystroke/scroll tick — same approach as
        // PlaceholderLayer.VisibleRange, guarded the same way. Falling back to the top of the
        // document keeps correctness independent of this optimization succeeding.
        int startChar = FirstVisibleCharIndex(tb);
        int lineNo = 1;
        int lineStart = 0;
        if (startChar > 0)
        {
            // A single linear scan for the logical line number + line-start index — vastly
            // cheaper than one GetRectFromCharacterIndex call per line.
            for (int i = 0; i < startChar && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lineNo++;
                    lineStart = i + 1;
                }
            }
        }

        // Walk LOGICAL lines from the first visible one: a wrapped line spans several display
        // lines but gets ONE number, drawn at the vertical position of its first character.
        while (true)
        {
            double? y = CharTop(tb, lineStart);
            if (y is { } top)
            {
                if (top > ActualHeight + 20) break;   // scrolled past the bottom: stop walking
                if (top >= -20)
                {
                    var ft = new FormattedText(lineNo.ToString(), CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, Face(), 11, NumBrush, dpi);
                    dc.DrawText(ft, new Point(Math.Max(0, ActualWidth - ft.Width - 6), top + 1));
                }
            }

            int nl = text.IndexOf('\n', lineStart);
            if (nl < 0) break;
            lineStart = nl + 1;
            lineNo++;
            if (lineStart > text.Length) break;
        }
    }

    /// <summary>First character index currently on screen, or -1 when it can't be determined
    /// (mid-relayout, or the display-line APIs fail) — the caller then falls back to walking from
    /// the top of the document. Mirrors <see cref="PlaceholderLayer.VisibleRange"/>: display line
    /// index is NOT the same as logical line index when soft wrap is on, so this only yields a
    /// character index, never a line number directly.</summary>
    private static int FirstVisibleCharIndex(TextBox tb)
    {
        try
        {
            int firstLine = tb.GetFirstVisibleLineIndex();
            if (firstLine < 0) return -1;
            int idx = tb.GetCharacterIndexFromLineIndex(firstLine);
            return idx < 0 ? -1 : idx;
        }
        catch (ArgumentOutOfRangeException) { return -1; }
    }

    /// <summary>Top of the line containing <paramref name="index"/>, in the TextBox's coordinate
    /// space (already scroll-adjusted). The gutter is laid out at the same height as the TextBox,
    /// so no offset math is needed. Null when WPF can't place it.</summary>
    private static double? CharTop(TextBox tb, int index)
    {
        try
        {
            if (index > tb.Text.Length) return null;
            var r = tb.GetRectFromCharacterIndex(index);
            if (double.IsInfinity(r.Y) || double.IsNaN(r.Y)) return null;
            return r.Y;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
