using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace QuickText.Core.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private readonly ResourceManager _rm =
        new("QuickText.Core.Localization.Strings", typeof(LocalizationService).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture => _culture;

    public string this[string key] => _rm.GetString(key, _culture) ?? key;

    public void SetCulture(string cultureName)
    {
        _culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(cultureName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
