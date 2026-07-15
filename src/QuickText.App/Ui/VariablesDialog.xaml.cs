using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QuickText.Core.Localization;

namespace QuickText.App.Ui;

public partial class VariablesDialog : Window
{
    public sealed class Field
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public List<string> Options { get; set; } = new();
        // Template picks the editor per field: plain TextBox, or an editable ComboBox
        // when the token declared options ({环境|dev|test|prod}).
        public Visibility TextBoxVis => Options.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ComboVis => Options.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private readonly ObservableCollection<Field> _fields = new();

    // Remember the last value entered per variable name (this session) so a repeated
    // expansion pre-fills instead of asking from scratch; the field is selected on focus,
    // so Enter accepts it and typing replaces it.
    private static readonly Dictionary<string, string> LastValues = new();

    public VariablesDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        var loc = LocalizationService.Instance;
        Title = loc["App.Name"];
        TitleText.Text = loc["Dialog.FillVariables"];
        OkButton.Content = loc["Search.Hint.Send"];
        CancelButton.Content = loc["Dialog.Cancel"];
        Fields.ItemsSource = _fields;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    public void Populate(IReadOnlyList<QuickText.Core.Snippets.Placeholders.VariableSpec> variables)
    {
        foreach (var v in variables)
            _fields.Add(new Field
            {
                Name = v.Name,
                // Session memory beats the token's declared default — repeats shouldn't re-ask.
                Value = LastValues.TryGetValue(v.Name, out var last) ? last : v.Default,
                Options = v.Options.ToList(),
            });
        Loaded += (_, _) => Dispatcher.BeginInvoke(new System.Action(FocusFirstField), DispatcherPriority.Loaded);
    }

    /// <summary>Prompt for the given variables. Returns name->value, or null if cancelled.</summary>
    public static Dictionary<string, string>? Fill(IReadOnlyList<QuickText.Core.Snippets.Placeholders.VariableSpec> variables)
    {
        var d = new VariablesDialog();
        d.Populate(variables);
        if (d.ShowDialog() != true) return null;
        var result = d._fields.ToDictionary(f => f.Name, f => f.Value ?? "");
        foreach (var kv in result) LastValues[kv.Key] = kv.Value;   // remember for next time
        return result;
    }

    private void FocusFirstField()
    {
        Fields.UpdateLayout();
        if (Fields.ItemContainerGenerator.ContainerFromIndex(0) is DependencyObject c
            && FindDescendant<TextBox>(c) is { } tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindDescendant<T>(child) is { } r) return r;
        }
        return null;
    }
}
