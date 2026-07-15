using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickText.App.Ui;

/// <summary>Attached behavior: render <c>Text</c> on a TextBlock with occurrences of <c>Query</c> accented.</summary>
public static class Highlight
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Highlight), new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(Highlight), new PropertyMetadata("", OnChanged));

    public static void SetText(DependencyObject o, string v) => o.SetValue(TextProperty, v);
    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetQuery(DependencyObject o, string v) => o.SetValue(QueryProperty, v);
    public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);

    private static void OnChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBlock tb) return;
        var text = GetText(tb) ?? "";
        var query = (GetQuery(tb) ?? "").Trim();

        tb.Inlines.Clear();
        if (query.Length == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        var accent = tb.TryFindResource("Brush.Accent") as Brush;
        int i = 0;
        while (i < text.Length)
        {
            int idx = text.IndexOf(query, i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { tb.Inlines.Add(new Run(text.Substring(i))); break; }
            if (idx > i) tb.Inlines.Add(new Run(text.Substring(i, idx - i)));
            var hit = new Run(text.Substring(idx, query.Length)) { FontWeight = FontWeights.Bold };
            if (accent != null) hit.Foreground = accent;
            tb.Inlines.Add(hit);
            i = idx + query.Length;
        }
    }
}
