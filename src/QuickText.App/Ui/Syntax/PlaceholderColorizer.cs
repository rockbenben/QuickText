using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui.Syntax;

/// <summary>
/// Paints placeholder tokens inside AvalonEdit. A second RENDERING of the same judgments —
/// the semantics still come from exactly one place (PlaceholderScanner.Scan), so what the code
/// editor shows can never disagree with what the plain-text editor shows or with what gets
/// pasted. Backgrounds only, so syntax colouring (which owns the foreground) stays readable.
/// </summary>
internal sealed class PlaceholderColorizer : DocumentColorizingTransformer
{
    private readonly Func<IReadOnlyList<TokenSpan>> _spans;
    private readonly Func<bool> _enabled;

    public PlaceholderColorizer(Func<IReadOnlyList<TokenSpan>> spans, Func<bool> enabled)
    {
        _spans = spans;
        _enabled = enabled;
    }

    private static readonly Brush VarBg = Frozen("#333DC2A0");
    private static readonly Brush AutoBg = Frozen("#33F2B457");
    private static readonly Brush NestBg = Frozen("#33A98CE8");
    private static readonly Brush CursorBg = Frozen("#223DC2A0");
    private static readonly Brush BadBg = Frozen("#33F2777A");
    private static readonly Brush BadUnderline = Frozen("#FFF2777A");

    // Hoisted out of ColorizeLine: a fresh TextDecorationCollection + Pen per invalid token, per
    // line, per redraw was pure allocation churn for a value that never varies.
    private static readonly TextDecorationCollection InvalidUnderline = FrozenDecorations();

    private static TextDecorationCollection FrozenDecorations()
    {
        var decorations = new TextDecorationCollection
        {
            new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Pen = new Pen(BadUnderline, 1.2),
            },
        };
        decorations.Freeze();
        return decorations;
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        b.Freeze();
        return b;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!_enabled()) return;               // placeholders off for this snippet: paint nothing
        var spans = _spans();
        if (spans.Count == 0 || line.Length == 0) return;

        int lineStart = line.Offset, lineEnd = line.EndOffset;
        foreach (var span in spans)
        {
            int start = span.Start, end = span.Start + span.Length;
            if (end <= lineStart || start >= lineEnd) continue;      // not on this line
            // A token can only be clipped by a line boundary when it is unclosed; clamp so a
            // partial token still gets its share rather than throwing.
            int from = Math.Max(start, lineStart);
            int to = Math.Min(end, lineEnd);
            if (to <= from) continue;

            var brush = span.Kind switch
            {
                TokenKind.Variable => VarBg,
                TokenKind.Auto => AutoBg,
                TokenKind.Nested => NestBg,
                TokenKind.Cursor => CursorBg,
                TokenKind.Invalid => BadBg,
                _ => null,
            };
            if (brush == null) continue;

            bool invalid = span.Kind == TokenKind.Invalid;
            ChangeLinePart(from, to, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(brush);
                if (invalid) element.TextRunProperties.SetTextDecorations(InvalidUnderline);
            });
        }
    }
}
