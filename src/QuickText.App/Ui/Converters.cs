using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickText.App;

namespace QuickText.App.Ui;

/// <summary>Snippet-relative image path -> a loaded, file-unlocked, frozen BitmapImage (null if missing).</summary>
public static class ImageLoader
{
    public static BitmapImage? Load(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        try
        {
            var abs = AppState.Current.ResolveImagePath(relativePath);
            if (!System.IO.File.Exists(abs)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;   // load fully so the file isn't locked
            bi.UriSource = new Uri(abs);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }
}

/// <summary>Relative image path -> a loaded, file-unlocked BitmapImage (null if missing).</summary>
public sealed class ImagePathToSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string rel ? ImageLoader.Load(rel) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
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
    private static readonly Brush Neutral = Freeze(new SolidColorBrush(Color.FromRgb(0x55, 0x5C, 0x68)));
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
