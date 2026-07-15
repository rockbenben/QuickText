using System.Windows;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

/// <summary>Themed replacement for InputBox / MessageBox — prompt, confirm, alert.</summary>
public partial class AppDialog : Window
{
    public AppDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string L(string key) => LocalizationService.Instance[key];

    /// <summary>Text input. Returns the entered string, or null if cancelled.</summary>
    public static string? Prompt(Window owner, string title, string label, string def = "")
    {
        var d = new AppDialog { Owner = owner, Title = title };
        d.MessageText.Text = label;
        d.InputBox.Text = def;
        d.OkButton.Content = L("Dialog.OK");
        d.CancelButton.Content = L("Dialog.Cancel");
        d.Loaded += (_, _) => { d.InputBox.Focus(); d.InputBox.SelectAll(); };
        return d.ShowDialog() == true ? d.InputBox.Text : null;
    }

    /// <summary>Two-button confirmation. Returns true if the primary button was chosen.</summary>
    public static bool Confirm(Window owner, string title, string message, string okText, string? cancelText = null)
    {
        var d = new AppDialog { Owner = owner, Title = title };
        d.MessageText.Text = message;
        d.InputBox.Visibility = Visibility.Collapsed;
        d.OkButton.Content = okText;
        d.CancelButton.Content = cancelText ?? L("Dialog.Cancel");
        return d.ShowDialog() == true;
    }

    /// <summary>Single-button message.</summary>
    public static void Alert(Window owner, string title, string message)
    {
        var d = new AppDialog { Owner = owner, Title = title };
        d.MessageText.Text = message;
        d.InputBox.Visibility = Visibility.Collapsed;
        d.CancelButton.Visibility = Visibility.Collapsed;
        d.OkButton.Content = L("Dialog.OK");
        d.ShowDialog();
    }
}
