using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickText.App.Ui;

/// <summary>
/// Attached behavior: render <c>Text</c> on a TextBlock with the matched run accented.
/// <para>Two modes. When <c>Start</c> is non-negative the search layer has told us exactly which
/// characters matched, and that range is accented verbatim — the only way to explain a pinyin hit,
/// where the query ("qj") appears nowhere in the text ("请假条"). Otherwise it falls back to
/// accenting literal occurrences of <c>Query</c>, which is still right for fields the matcher does
/// not report spans for, such as the body line.</para>
/// </summary>
public static class Highlight
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Highlight), new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(Highlight), new PropertyMetadata("", OnChanged));

    /// <summary>Start of the matched run, or -1 when the matcher could not pin one down.</summary>
    public static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached(
        "Start", typeof(int), typeof(Highlight), new PropertyMetadata(-1, OnChanged));

    public static readonly DependencyProperty LengthProperty = DependencyProperty.RegisterAttached(
        "Length", typeof(int), typeof(Highlight), new PropertyMetadata(0, OnChanged));

    public static void SetText(DependencyObject o, string v) => o.SetValue(TextProperty, v);
    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetQuery(DependencyObject o, string v) => o.SetValue(QueryProperty, v);
    public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);
    public static void SetStart(DependencyObject o, int v) => o.SetValue(StartProperty, v);
    public static int GetStart(DependencyObject o) => (int)o.GetValue(StartProperty);
    public static void SetLength(DependencyObject o, int v) => o.SetValue(LengthProperty, v);
    public static int GetLength(DependencyObject o) => (int)o.GetValue(LengthProperty);

    private static void OnChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBlock tb) return;
        var text = GetText(tb) ?? "";
        var query = (GetQuery(tb) ?? "").Trim();

        tb.Inlines.Clear();
        var accent = tb.TryFindResource("Brush.Accent") as Brush;

        // Bindings arrive in no guaranteed order, so every pass re-reads all four values and
        // rebuilds from scratch — never assume Start landed before or after Text.
        int start = GetStart(tb), length = GetLength(tb);
        if (start >= 0 && length > 0 && start + length <= text.Length)
        {
            if (start > 0) tb.Inlines.Add(new Run(text.Substring(0, start)));
            tb.Inlines.Add(Accented(text.Substring(start, length), accent));
            if (start + length < text.Length) tb.Inlines.Add(new Run(text.Substring(start + length)));
            return;
        }

        if (query.Length == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        int i = 0;
        while (i < text.Length)
        {
            int idx = text.IndexOf(query, i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { tb.Inlines.Add(new Run(text.Substring(i))); break; }
            if (idx > i) tb.Inlines.Add(new Run(text.Substring(i, idx - i)));
            tb.Inlines.Add(Accented(text.Substring(idx, query.Length), accent));
            i = idx + query.Length;
        }
    }

    private static Run Accented(string s, Brush? accent)
    {
        var run = new Run(s) { FontWeight = FontWeights.Bold };
        if (accent != null) run.Foreground = accent;
        return run;
    }
}
