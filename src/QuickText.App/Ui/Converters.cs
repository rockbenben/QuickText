using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickText.App;

namespace QuickText.App.Ui;

/// <summary>Snippet-relative image path -> a loaded, file-unlocked, frozen BitmapImage (null if
/// missing or outside the library). Decoded images are cached by path + file stamp; callers that
/// run on the UI thread should use <see cref="LoadAsync"/>.</summary>
public static class ImageLoader
{
    private sealed record Entry(DateTime LastWriteUtc, long Length, BitmapImage Image);
    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private const int MaxCachedImages = 128;

    public static BitmapImage? Load(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        try
        {
            var abs = AppState.Current.ResolveImagePath(relativePath);
            if (string.IsNullOrEmpty(abs)) return null;
            var info = new System.IO.FileInfo(abs);
            if (!info.Exists) return null;
            var stamp = info.LastWriteTimeUtc;
            long length = info.Length;
            lock (Gate)
            {
                if (Cache.TryGetValue(abs, out var hit) && hit.LastWriteUtc == stamp && hit.Length == length)
                    return hit.Image;
            }

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;   // load fully so the file isn't locked
            bi.UriSource = new Uri(abs);
            bi.EndInit();
            bi.Freeze();

            lock (Gate)
            {
                if (Cache.Count >= MaxCachedImages) Cache.Clear();
                Cache[abs] = new Entry(stamp, length, bi);
            }
            return bi;
        }
        catch { return null; }
    }

    public static Task<BitmapImage?> LoadAsync(string relativePath) =>
        Task.Run(() => Load(relativePath));
}

/// <summary>Attached behavior: decode the image path off the UI thread and assign it when ready.
/// A bound converter would run the full disk decode inside layout, which is how selecting a
/// OneDrive placeholder froze the window.</summary>
public static class AsyncImage
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(AsyncImage), new PropertyMetadata("", OnPathChanged));

    public static void SetPath(DependencyObject o, string v) => o.SetValue(PathProperty, v);
    public static string GetPath(DependencyObject o) => (string)o.GetValue(PathProperty);

    private static async void OnPathChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Image image) return;
        var path = e.NewValue as string ?? "";
        image.Source = null;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var bitmap = await ImageLoader.LoadAsync(path);
            if (!string.Equals(GetPath(image), path, StringComparison.Ordinal)) return;   // a newer path won
            image.Source = bitmap;
        }
        catch { /* a missing/locked image is already represented by a null source */ }
    }
}

/// <summary>Non-empty string -> Collapsed, empty -> Visible (inverse of EmptyToCollapsed).</summary>
public sealed class NonEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>#RRGGBB string -> brush; empty/invalid -> a neutral dot color.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    // Resolved from the palette rather than frozen at first use, so the light theme reaches it.
    // The literal stays as the fallback for contexts with no Application (unit tests, --smoke).
    private static Brush Neutral =>
        Application.Current?.TryFindResource("Brush.Neutral") as Brush ?? Fallback;
    private static readonly Brush Fallback = Freeze(new SolidColorBrush(Color.FromRgb(0x55, 0x5C, 0x68)));
    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try { return Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(s))); }
            catch { }
        }
        return Neutral;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Snippet id -> Visible when it is a favorite, else Collapsed.</summary>
public sealed class FavoriteVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string id && AppState.Current.IsFavorite(id) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Body text -> first non-empty line, trimmed, for a single-line preview.</summary>
public sealed class FirstLineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string ?? "";
        int nl = s.IndexOfAny(new[] { '\r', '\n' });
        if (nl >= 0) s = s.Substring(0, nl);
        return s.Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>List index -> quick-send badge "1".."9" for the first nine rows, else empty.</summary>
public sealed class IndexBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i >= 0 && i < 9 ? (i + 1).ToString() : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Empty/whitespace string -> Collapsed, otherwise Visible.</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Resolves a piece of user text to the <see cref="FlowDirection"/> it should be rendered in, so an
/// RTL layout does not re-order LTR content (and vice versa) — see <see cref="QuickText.Core.BidiText"/>.
/// Bound to a TextBlock's FlowDirection; the element's POSITION still follows the surrounding
/// layout, only the text inside it reads in its own direction.
/// </summary>
public sealed class ContentFlowDirectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => QuickText.Core.BidiText.IsRightToLeft(value as string)
            ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
