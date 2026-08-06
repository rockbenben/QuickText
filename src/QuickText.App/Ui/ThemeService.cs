using System.Windows;

namespace QuickText.App.Ui;

/// <summary>
/// Dark ⇄ light, by merging <c>Theme.Light.xaml</c> on top of <c>Theme.xaml</c> (and removing it
/// again). The light file carries only the keys that differ, so the dark palette stays the single
/// design of record.
/// <para>The obvious-looking shortcut — keep one brush instance and assign <c>.Color</c> — does not
/// work here: WPF freezes Freezables in a BAML-compiled ResourceDictionary, and re-freezes any
/// replacement put back into it, so every assignment is silently ignored. That is also why every
/// <c>Brush.*</c> reference in the XAML is a <c>DynamicResource</c>: StaticResource resolves once at
/// load and would keep already-open windows on the old palette.</para>
/// </summary>
public static class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";
    // Named FollowSystem, not System: a const called System inside this class shadows the root
    // namespace, and every System.* reference in the file stops compiling.
    public const string FollowSystem = "system";

    /// <summary>True when the resolved theme is light (a "system" setting reads the OS preference).</summary>
    public static bool IsLight { get; private set; }

    /// <summary>Raised after the palette changes, for the surfaces that read colours once instead of
    /// binding to a brush (the code editor's caret/selection and its syntax definitions).</summary>
    public static event Action? Changed;

    private static ResourceDictionary? _light;

    public static string Resolve(string setting) =>
        setting == Light ? Light :
        setting == FollowSystem ? (SystemPrefersLight() ? Light : Dark) :
        Dark;

    /// <summary>Windows' "app theme" preference; falls back to dark (the app's own default).</summary>
    private static bool SystemPrefersLight()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch { return false; }
    }

    public static void Apply(string setting)
    {
        var res = Application.Current?.Resources;
        if (res == null) return;

        bool light = Resolve(setting) == Light;
        IsLight = light;

        _light ??= new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Ui/Theme.Light.xaml", UriKind.Absolute)
        };

        bool merged = res.MergedDictionaries.Contains(_light);
        if (light && !merged) res.MergedDictionaries.Add(_light);
        else if (!light && merged) res.MergedDictionaries.Remove(_light);

        Changed?.Invoke();
    }
}
