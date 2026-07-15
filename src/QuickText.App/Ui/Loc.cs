using QuickText.Core.Localization;

namespace QuickText.App;

/// <summary>Bindable proxy so XAML can do {Binding [Key], Source={x:Static app:LocProxy.Instance}}.</summary>
public sealed class LocProxy
{
    public static LocalizationService Instance => LocalizationService.Instance;
}
