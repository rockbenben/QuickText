using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QuickText.Core.Localization;
using QuickText.Core.Models;

namespace QuickText.App.Ui;

/// <summary>Soft-deleted snippets: restore to their original category, delete for good, or empty all.</summary>
public partial class TrashDialog : Window
{
    private sealed class Row
    {
        public TrashEntry Entry { get; init; } = null!;
        public string Name => Entry.Snippet.Name;
        public string Detail { get; init; } = "";
        public string Preview { get; init; } = "";   // first line of the body ("[图片]" for images)
        public string Tip { get; init; } = "";       // tooltip: body head, for a fuller look
    }

    private static (string Preview, string Tip) PreviewOf(Snippet sn)
    {
        if (sn.IsImage) return ("🖼", "🖼");
        var body = sn.Body ?? "";
        int nl = body.IndexOfAny(new[] { '\r', '\n' });
        var first = (nl >= 0 ? body[..nl] : body).Trim();
        if (first.Length > 80) first = first[..80] + "…";
        var tip = body.Length <= 500 ? body : body[..500] + " …";
        return (first, tip);
    }

    private List<TrashEntry> _trash = new();

    public TrashDialog()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        WindowTheming.ApplyFlowDirection(this);
        var loc = LocalizationService.Instance;
        Title = loc["App.Name"];
        TitleText.Text = loc["Manager.Trash"];
        HintText.Text = string.Format(loc["Trash.Hint"], Core.Persistence.Store.TrashRetentionDays);
        RestoreButton.Content = loc["Trash.Restore"];
        DeleteButton.Content = loc["Trash.DeleteForever"];
        EmptyButton.Content = loc["Trash.Empty"];
        CloseButton.Content = loc["Trash.Close"];
        EmptyText.Text = loc["Trash.EmptyState"];
        Reload();
    }

    private void Reload()
    {
        var state = AppState.Current;
        state.MarkSelfWrite();   // LoadTrash may purge expired entries and write back
        _trash = state.Store.LoadTrash();
        Items.ItemsSource = _trash
            .OrderByDescending(t => t.DeletedAt)
            .Select(t =>
            {
                var (preview, tip) = PreviewOf(t.Snippet);
                return new Row
                {
                    Entry = t,
                    Detail = $"{t.Category} · {t.DeletedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
                    Preview = preview,
                    Tip = tip,
                };
            })
            .ToList();
        bool empty = _trash.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        RestoreButton.IsEnabled = DeleteButton.IsEnabled = EmptyButton.IsEnabled = !empty;
    }

    private List<TrashEntry> SelectedEntries() =>
        Items.SelectedItems.Cast<Row>().Select(r => r.Entry).ToList();

    private void OnRestore(object s, RoutedEventArgs e)
    {
        var selected = SelectedEntries();
        if (selected.Count == 0) return;
        var state = AppState.Current;
        var cats = state.Store.LoadAll();
        // A crash mid-delete can leave a snippet in both the library and the trash; restoring
        // such an entry must not create a duplicate Id — just drop the trash copy.
        var existingIds = new HashSet<string>(cats.SelectMany(c => c.Snippets).Select(s => s.Id));

        foreach (var group in selected.GroupBy(t => t.Category))
        {
            var toAdd = group.Where(t => existingIds.Add(t.Snippet.Id)).ToList();
            if (toAdd.Count == 0) continue;
            var cat = cats.FirstOrDefault(c => c.Name == group.Key);
            if (cat == null)
            {
                // Original category is gone — recreate it so the snippet lands where it lived.
                cat = new Category { Name = group.Key.Length > 0 ? group.Key : LocalizationService.Instance["Manager.Categories"] };
                cats.Add(cat);
            }
            foreach (var t in toAdd) cat.Snippets.Add(t.Snippet);
            state.MarkSelfWrite();
            state.Store.SaveCategory(cat);
        }

        _trash.RemoveAll(selected.Contains);
        state.MarkSelfWrite();
        state.Store.SaveTrash(_trash);
        state.ReloadData();
        Reload();
    }

    private void OnDeleteForever(object s, RoutedEventArgs e)
    {
        var selected = SelectedEntries();
        if (selected.Count == 0) return;
        PurgeEntries(selected);
    }

    private void OnEmptyTrash(object s, RoutedEventArgs e)
    {
        if (_trash.Count == 0) return;
        var loc = LocalizationService.Instance;
        if (!AppDialog.Confirm(this, loc["App.Name"], loc["Trash.EmptyConfirm"], loc["Trash.Empty"])) return;
        PurgeEntries(_trash.ToList());
    }

    private void PurgeEntries(List<TrashEntry> entries)
    {
        var state = AppState.Current;
        foreach (var t in entries)
            if (t.Snippet.IsImage) state.Store.DeleteImage(t.Snippet.ImagePath);
        _trash.RemoveAll(entries.Contains);
        state.MarkSelfWrite();
        state.Store.SaveTrash(_trash);
        Reload();
    }

    private void OnClose(object s, RoutedEventArgs e) => Close();
}
