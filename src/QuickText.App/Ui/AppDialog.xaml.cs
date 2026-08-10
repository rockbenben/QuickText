using System.Windows;
using System.Windows.Input;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

/// <summary>Themed replacement for InputBox / MessageBox — prompt, confirm, alert.</summary>
public partial class AppDialog : Window
{
    public AppDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.CapHeightToMonitor(this);   // SizeToContent=Height + NoResize: bound it or the OK button leaves the screen
        WindowTheming.ApplyFlowDirection(this);
        // Esc always means cancel — never set DialogResult here, so ShowDialog() returns null
        // exactly as it does when the window is closed via its native close button (X). Both
        // Prompt and Confirm already treat null the same as their explicit Cancel-button result
        // (both check "== true"), so this is a no-op for them and is what lets the three-way
        // ConfirmSaveDiscard tell "cancel" (null) apart from "discard" (false).
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        };
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    // Leaves DialogResult unset (null) rather than false — see the Esc comment above.
    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnDiscard(object sender, RoutedEventArgs e) => DialogResult = false;

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

    /// <summary>Two-button confirmation. Returns true if the primary button was chosen.
    /// <para>The confirm button carries DANGER styling, not the accent: every caller of this method
    /// asks about something irreversible (delete a category and its snippets, overwrite the data
    /// folder from a backup, purge the trash), and the trash dialog already paints exactly those
    /// actions red. Confirming in the same green used for Save / Send / Restore told the reader the
    /// opposite of what the button does. If a genuinely benign confirmation ever needs this method,
    /// give it a parameter rather than quietly repainting the destructive ones.</para></summary>
    public static bool Confirm(Window owner, string title, string message, string okText, string? cancelText = null)
    {
        var d = new AppDialog { Owner = owner, Title = title };
        d.MessageText.Text = message;
        d.InputBox.Visibility = Visibility.Collapsed;
        d.OkButton.Style = (Style)d.FindResource("DarkButtonDanger");
        d.OkButton.Content = okText;
        d.CancelButton.Content = cancelText ?? L("Dialog.Cancel");
        return d.ShowDialog() == true;
    }

    /// <summary>Three-way question (e.g. Save / Don't save / Cancel). Returns true if
    /// <paramref name="saveText"/> was chosen, false for <paramref name="discardText"/>, or null
    /// if the user cancelled (Esc or the window's close button) — cancel must never be treated
    /// as a silent discard by the caller.
    /// <para>Discard carries DANGER styling for the same reason <see cref="Confirm"/>'s OK does:
    /// it is the button that throws the user's unsaved edits away. Left in the default grey it was
    /// pixel-identical to Cancel right beside it — two buttons that look the same and do opposite
    /// things, in the one dialog where the wrong click costs work. It is styled in the XAML rather
    /// than here because DiscardButton exists for this method alone (OkButton cannot be: Prompt and
    /// Alert need it primary).</para></summary>
    public static bool? ConfirmSaveDiscard(Window owner, string title, string message,
                                            string saveText, string discardText, string cancelText)
    {
        var d = new AppDialog { Owner = owner, Title = title };
        d.MessageText.Text = message;
        d.InputBox.Visibility = Visibility.Collapsed;
        d.OkButton.Content = saveText;
        d.DiscardButton.Content = discardText;
        d.DiscardButton.Visibility = Visibility.Visible;
        d.CancelButton.Content = cancelText;
        return d.ShowDialog();
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
