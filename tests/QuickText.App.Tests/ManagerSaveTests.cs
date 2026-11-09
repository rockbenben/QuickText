using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickText.App;
using QuickText.App.Ui;
using QuickText.Core;
using QuickText.Core.Models;
using QuickText.Core.Persistence;

namespace QuickText.App.Tests;

public class ManagerSaveTests
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ScreenRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out ScreenRect rect);

    // Keep one Application on one STA thread. The host deliberately bypasses production startup,
    // so pumping modal dialogs cannot install hooks or touch the user's running instance.
    private sealed class TestApp : Application
    {
        protected override void OnStartup(StartupEventArgs e) { }
        protected override void OnExit(ExitEventArgs e) { }
    }

    [Fact]
    public void Library_workflows_preserve_saved_data_and_retry_state_on_the_STA_dispatcher()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "QuickText-manager-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, AppPaths.PortableMarkerName), "");
            AppPaths.SetExeDir(root);
            AppPaths.PinPortableState();
            var app = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            try
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/QuickText;component/Ui/Theme.xaml", UriKind.Relative)
                });
                foreach (var action in new[] { "save", "snippet", "category", "close", "structural", "discard", "discard-locked", "partial" })
                    CheckAction(root, action);
                CheckUndo(root, lockTrashCleanup: false);
                CheckUndo(root, lockTrashCleanup: true);
                foreach (var action in new[] { "replace", "remove", "discard", "stale", "replace-twice", "capture" })
                    CheckImage(root, action);
                CheckLibraryChanges(root);
                CheckLayout(root);
                CheckMove(root, lockDestination: true);
                CheckMove(root, lockDestination: false);
                CheckTrashDuringCapture(root);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                AppState.Current.StopWatching();
                // Cleanup must never retry a test draft after an assertion fails.
                typeof(App).GetProperty("InSmoke", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, true);
                foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
                app.Shutdown();
                Directory.Delete(root, recursive: true);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "Manager regression test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void CheckAction(string root, string action)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, action);
        var store = new Store(state.Settings.DataFolder);
        var first = new Snippet { Name = "First", Body = "saved body" };
        store.SaveCategory(new Category { Name = "A", Snippets = new() { first, new() { Name = "Second", Body = "second" } } });
        store.SaveCategory(new Category { Name = "B", Snippets = new() { new() { Name = "Other", Body = "other" } } });
        state.ReloadData();
        var manager = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
        manager.SourceInitialized += (_, _) => { manager.Left = -32000; manager.Top = -32000; };
        manager.Show();
        manager.SelectSnippet(first.Id);
        var body = (BodyEditor)manager.FindName("Body");
        var snippets = (ListBox)manager.FindName("Snippets");
        var categories = (ListBox)manager.FindName("Categories");
        if (action == "structural") manager.AddSnippet("Draft", "draft body");
        else body.Text = "draft body";
        if (action == "partial")
        {
            var cats = (List<Category>)typeof(ManagerWindow).GetField("_cats", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            cats[1].Snippets[0].Body = "saved independently";
            var dirty = (HashSet<Category>)typeof(ManagerWindow).GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            dirty.Add(cats[1]);
        }

        string file = Path.Combine(store.Root, store.LoadIndex().Categories[0].File);
        int savedVersion = state.DataVersion;
        using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var choices = action is "snippet" or "category" or "close" or "structural"
                ? new[] { "OkButton", "OkButton" } : new[] { "OkButton" };
            using (var dialogs = new DialogAnswers(choices))
            {
                switch (action)
                {
                    case "snippet": snippets.SelectedIndex = 1; break;
                    case "category": categories.SelectedIndex = 1; break;
                    case "close": case "structural": manager.Close(); break;
                    default: Save(manager); break;
                }
            }
            Assert.True(manager.IsVisible, action);
            Assert.Equal(0, categories.SelectedIndex);
            Assert.Equal(action == "structural" ? "Draft" : "First", ((Snippet)snippets.SelectedItem).Name);
            Assert.Equal("draft body", body.Text);
            Assert.Equal("saved body", state.Search.Search("First").Single().Snippet.Body);
            Assert.DoesNotContain(state.Search.Search("draft body"), h => h.Snippet.Body == "draft body");
            if (action == "partial") Assert.Equal("saved independently", state.Search.Search("Other").Single().Snippet.Body);
            else Assert.Equal(savedVersion, state.DataVersion);
            if (action == "discard-locked")
            {
                using var dialogs = new DialogAnswers("DiscardButton");
                manager.Close();
                Assert.False(manager.IsVisible);
            }
        }
        Assert.Equal("saved body", store.LoadAll()[0].Snippets[0].Body);
        if (action == "discard-locked")
        {
            Assert.Equal(first.UpdatedAt, store.LoadAll()[0].Snippets[0].UpdatedAt);
            return;
        }

        if (action == "discard")
        {
            using (var dialogs = new DialogAnswers("DiscardButton")) snippets.SelectedIndex = 1;
            manager.Close();
            Assert.Equal("saved body", store.LoadAll()[0].Snippets[0].Body);
            Assert.Equal("saved body", state.Search.Search("First").Single().Snippet.Body);
        }
        else
        {
            if (action is "snippet" or "category" or "close")
            {
                using var dialogs = new DialogAnswers("OkButton");
                if (action == "snippet") snippets.SelectedIndex = 1;
                else if (action == "category") categories.SelectedIndex = 1;
                else manager.Close();
            }
            else if (action == "structural")
            {
                using var dialogs = new DialogAnswers("OkButton");
                manager.Close();
            }
            else Save(manager);
            var saved = store.LoadAll()[0].Snippets.Single(s => s.Name == (action == "structural" ? "Draft" : "First"));
            Assert.Equal("draft body", saved.Body);
            Assert.Equal(saved.Body, state.Search.Search(saved.Name).Single().Snippet.Body);
            if (manager.IsVisible) manager.Close();
        }
        Assert.False(manager.IsVisible);
    }

    private static void Save(ManagerWindow manager) =>
        ((Button)manager.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void CheckUndo(string root, bool lockTrashCleanup)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, lockTrashCleanup ? "undo-trash-locked" : "undo-category-locked");
        var store = new Store(state.Settings.DataFolder);
        var deleted = new Snippet { Name = "RestoreMe", Body = "recoverable body" };
        var keep = new Snippet { Name = "KeepMe", Body = "unaffected body" };
        store.SaveCategory(new Category { Name = "Undo", Snippets = new() { deleted, keep } });
        state.ReloadData();
        var manager = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
        manager.SourceInitialized += (_, _) => { manager.Left = -32000; manager.Top = -32000; };
        manager.Show();
        manager.SelectSnippet(deleted.Id);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        typeof(ManagerWindow).GetMethod("OnDeleteSnippet", flags)!.Invoke(manager, new object[] { manager, new RoutedEventArgs() });
        Assert.DoesNotContain(store.LoadAll()[0].Snippets, s => s.Id == deleted.Id);
        Assert.Equal(deleted.Id, Assert.Single(store.LoadTrash()).Snippet.Id);
        byte[] trashBefore = File.ReadAllBytes(Path.Combine(store.Root, "trash.json"));

        void Undo() => typeof(ManagerWindow).GetMethod("UndoDelete", flags)!.Invoke(manager, null);
        string categoryFile = Path.Combine(store.Root, store.LoadIndex().Categories[0].File);
        // Locking the cleanup temp file keeps trash readable while exercising an actual write
        // failure after the restored category has already reached disk.
        string lockedFile = lockTrashCleanup ? Path.Combine(store.Root, "trash.json.tmp") : categoryFile;
        using (var locked = new FileStream(lockedFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            using (var dialogs = new DialogAnswers("OkButton")) Undo();
            Assert.Equal(trashBefore, File.ReadAllBytes(Path.Combine(store.Root, "trash.json")));
            Assert.Equal(deleted.Id, Assert.Single(new Store(store.Root).LoadTrash()).Snippet.Id);
            var matches = state.Search.Search(deleted.Name);
            Assert.Equal(lockTrashCleanup ? 1 : 0, matches.Count);
            if (lockTrashCleanup)
                Assert.Single(store.LoadAll()[0].Snippets, s => s.Id == deleted.Id);
        }

        var beforeRetry = store.LoadAll()[0].Snippets.Where(s => s.Id == deleted.Id).ToList();
        Assert.Equal(lockTrashCleanup ? 1 : 0, beforeRetry.Count);
        Undo();
        Assert.Empty(store.LoadTrash());
        // A second Ctrl+Z after success must leave the restored category unchanged.
        Undo();
        var afterRetry = new Store(store.Root).LoadAll()[0].Snippets;
        Assert.Equal(new[] { deleted.Id, keep.Id }, afterRetry.Select(s => s.Id));
        Assert.Equal(deleted.Body, afterRetry[0].Body);
        Assert.Equal(keep.Body, afterRetry[1].Body);
        Assert.Equal(deleted.Body, Assert.Single(state.Search.Search(deleted.Name)).Snippet.Body);
        manager.Close();
        Assert.False(manager.IsVisible);
    }

    private static void CheckImage(string root, string action)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, "image-" + action);
        var store = new Store(state.Settings.DataFolder);
        var old = store.SaveImage(new byte[] { 1, 2, 3 }, ".png");
        var sn = new Snippet { Name = "Image", Body = "body", ImagePath = old };
        store.SaveCategory(new Category { Name = "Images", Snippets = new() { sn } });
        state.ReloadData();
        var manager = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
        manager.SourceInitialized += (_, _) => { manager.Left = -32000; manager.Top = -32000; };
        manager.Show();
        manager.SelectSnippet(sn.Id);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        if (action == "remove")
            typeof(ManagerWindow).GetMethod("OnImageRemove", flags)!.Invoke(manager, new object[] { manager, new RoutedEventArgs() });
        else
            typeof(ManagerWindow).GetMethod("SetImage", flags)!.Invoke(manager, new object[] { new byte[] { 4, 5, 6 }, ".png" });
        var editing = (Snippet)typeof(ManagerWindow).GetField("_editing", flags)!.GetValue(manager)!;
        var firstDraft = editing.ImagePath;
        if (action == "replace-twice")
            typeof(ManagerWindow).GetMethod("SetImage", flags)!.Invoke(manager, new object[] { new byte[] { 7, 8, 9 }, ".png" });
        string draft = editing.ImagePath;
        if (action == "capture")
        {
            Assert.True(Capture(manager, "SilentCapture", "captured body"));
            Assert.Equal(old, store.LoadAll()[0].Snippets[0].ImagePath);
            Assert.Equal(draft, editing.ImagePath);
            Assert.Single(store.LoadAll()[0].Snippets, s => s.Name == "SilentCapture");
        }
        Assert.True(File.Exists(store.ResolveImage(old)));
        using (var locked = new FileStream(Path.Combine(store.Root, "Images.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            using (var dialogs = new DialogAnswers("OkButton")) Save(manager);
            Assert.Equal(old, Assert.Single(state.Search.Search(sn.Name)).Snippet.ImagePath);
            Assert.True(File.Exists(store.ResolveImage(old)));
        }
        Assert.Equal(old, store.LoadAll()[0].Snippets[0].ImagePath);
        if (action == "discard")
        {
            using var dialogs = new DialogAnswers("DiscardButton");
            manager.Close();
        }
        else if (action == "stale")
        {
            state.ApplyCategories(state.SnapshotCategories());
            using var dialogs = new DialogAnswers("OkButton");
            manager.Close();
        }
        else
        {
            Save(manager);
            Assert.Equal(draft, store.LoadAll()[0].Snippets[0].ImagePath);
            Assert.Equal(draft, Assert.Single(state.Search.Search(sn.Name)).Snippet.ImagePath);
            Assert.False(File.Exists(store.ResolveImage(old)));
            manager.Close();
        }
        if (action is "discard" or "stale")
        {
            Assert.Equal(old, store.LoadAll()[0].Snippets[0].ImagePath);
            Assert.True(File.Exists(store.ResolveImage(old)));
            Assert.False(File.Exists(store.ResolveImage(draft)));
        }
        if (action == "replace-twice") Assert.False(File.Exists(store.ResolveImage(firstDraft)));
    }

    private static bool Capture(ManagerWindow manager, string name, string body)
    {
        var task = manager.TryAddCapturedSnippetAsync(name, body);
        Await(task);
        return task.GetAwaiter().GetResult();
    }

    private static void Await(Task task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    private static void CheckLibraryChanges(string root)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, "reload-A");
        var old = new Store(state.Settings.DataFolder);
        old.SaveCategory(new Category { Name = "A", Snippets = new() { new() { Name = "Old", Body = "old" } } });
        state.ReloadData();
        int version = state.DataVersion;
        var oldCats = old.LoadAll();
        var current = new Store(Path.Combine(root, "reload-B"));
        current.SaveCategory(new Category { Name = "B", Snippets = new() { new() { Name = "New", Body = "new" } } });
        Await(state.SaveSettingsAsync(new() { DataFolder = current.Root }));
        var apply = typeof(AppState).GetMethod("TryApplyReload", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)apply.Invoke(state, new object[] { old.Root, version, oldCats, old, true })!);
        Assert.Equal(current.Root, state.Store.Root);
        Assert.Single(state.Search.Search("New"));
        int localVersion = state.DataVersion;
        state.ApplyCategories(new List<Category> { new() { Name = "B", Snippets = new() { new() { Name = "SavedLater", Body = "later" } } } });
        Assert.False((bool)apply.Invoke(state, new object[] { current.Root, localVersion, current.LoadAll(), current, true })!);
        Assert.Single(state.Search.Search("SavedLater"));
        // Restore disk-backed state before testing capture and failed settings changes.
        state.ReloadData();
        var writeGate = typeof(Store).GetField("_writeGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state.Store)!;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = new Thread(() =>
        {
            lock (writeGate)
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(5));
            }
        }) { IsBackground = true };
        writer.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var captures = Task.WhenAll(state.CaptureClipboardAsync("Capture1", "one"), state.CaptureClipboardAsync("Capture2", "two"));
            Assert.False(captures.IsCompleted);
            Assert.True(state.IsLibraryBusy);
            bool dispatcherRan = false;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { dispatcherRan = true; release.Set(); }));
            Await(captures);
            Assert.True(dispatcherRan);
            Assert.All(captures.Result, sn => Assert.NotNull(sn));
            Assert.False(state.IsLibraryBusy);
        }
        finally
        {
            release.Set();
            Assert.True(writer.Join(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(3, current.LoadAll()[0].Snippets.Count);

        state.StartWatching();
        state.MarkSelfWrite(600);
        var external = current.LoadAll()[0];
        external.Snippets.Add(new Snippet { Name = "ExternalDuringSuppression", Body = "external" });
        current.SaveCategory(external);
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!state.Categories.SelectMany(c => c.Snippets).Any(s => s.Name == "ExternalDuringSuppression") && DateTime.UtcNow < deadline)
            Await(Task.Delay(50));
        Assert.Single(state.Search.Search("ExternalDuringSuppression"));
        state.StopWatching();
        int unchangedVersion = state.DataVersion;
        string categoryPath = Path.Combine(current.Root, current.LoadIndex().Categories[0].File);
        File.AppendAllText(categoryPath, "\n\n");
        Await(state.ReloadDataAsync());
        Assert.Equal(unchangedVersion, state.DataVersion);
        Await(state.CaptureClipboardAsync("AfterFormatting", "formatting only"));
        string settingsBefore = File.ReadAllText(AppPaths.SettingsPath);
        var unavailable = Path.Combine(root, "not-a-directory");
        File.WriteAllText(unavailable, "blocked");
        Assert.ThrowsAny<IOException>(() => Await(state.SaveSettingsAsync(new() { DataFolder = unavailable })));
        Assert.Equal(settingsBefore, File.ReadAllText(AppPaths.SettingsPath));
        Assert.Equal(current.Root, state.Store.Root);
        Assert.Equal(current.Root, state.Settings.DataFolder);
        var invalid = new Store(Path.Combine(root, "invalid-import"));
        Directory.CreateDirectory(invalid.Root);
        File.WriteAllText(Path.Combine(invalid.Root, "index.json"), "{\"Categories\":[{\"Name\":\"Missing\",\"File\":\"missing.json\"}]}");
        byte[] index = File.ReadAllBytes(Path.Combine(current.Root, "index.json"));
        var importing = state.ImportLibraryAsync(invalid.Root);
        Assert.True(state.IsLibraryBusy);
        var blocked = state.CaptureClipboardAsync("Blocked", "blocked");
        Assert.True(state.IsLibraryBusy);
        Await(blocked);
        Assert.Null(blocked.Result);
        Assert.Throws<InvalidDataException>(() => Await(importing));
        Assert.False(state.IsLibraryBusy);
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(current.Root, "index.json")));
        Assert.Equal(5, current.LoadAll()[0].Snippets.Count);

        var valid = new Store(Path.Combine(root, "valid-import"));
        valid.SaveCategory(new Category { Name = "AImported", Snippets = new() { new() { Name = "Imported", Body = "validated" } } });
        using (var locked = new FileStream(Path.Combine(current.Root, "index.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => Await(state.ImportLibraryAsync(valid.Root)));
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(current.Root, "index.json")));
        Assert.False(File.Exists(Path.Combine(current.Root, "AImported.json")));
        Assert.Equal(5, state.Categories[0].Snippets.Count);
        Await(state.ImportLibraryAsync(valid.Root));
        Assert.Single(state.Search.Search("Imported"));
        Assert.Equal("AImported", Assert.Single(state.Store.LoadAll(strict: true)).Name);

        // A library at the machine-state root must not migrate usage.stats onto itself.
        state.Settings.DataFolder = AppPaths.MachineStateDir;
        new Store(state.Settings.DataFolder).SaveCategory(new Category { Name = "Usage" });
        File.WriteAllText(Path.Combine(AppPaths.MachineStateDir, "usage.stats"), "{\"Favorites\":[\"kept\"]}");
        state.ReloadData();
        Assert.True(state.Usage.IsFavorite("kept"));
        Assert.True(File.Exists(Path.Combine(AppPaths.MachineStateDir, "usage.stats")));
        var pendingUsage = state.Usage;
        pendingUsage.Record("pending");
        pendingUsage.ToggleFavorite("pending");
        using (var locked = new FileStream(Path.Combine(AppPaths.MachineStateDir, "usage.stats"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            state.ReloadData(notify: false);
            Assert.Same(pendingUsage, state.Usage);
        }
        Assert.True(state.Usage.Flush());
        var savedUsage = new UsageStore(AppPaths.MachineStateDir);
        Assert.Equal(1, savedUsage.CountOf("pending"));
        Assert.True(savedUsage.IsFavorite("pending"));
    }

    private static void CheckLayout(string root)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, "layout");
        new Store(state.Settings.DataFolder).SaveCategory(new Category { Name = "Layout", Snippets = new() { new() { Name = "Entry", Body = "body" } } });
        state.ReloadData();
        foreach (var language in new[] { "en", "de", "ar" })
        {
            Core.Localization.LocalizationService.Instance.SetCulture(language);
            var manager = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
            manager.SourceInitialized += (_, _) => { manager.Left = -32000; manager.Top = -32000; manager.MaxHeight = 464; };
            manager.Show();
            ((CheckBox)manager.FindName("ImageToggle")).IsChecked = true;
            manager.UpdateLayout();
            var button = (Button)manager.FindName("SaveButton");
            Assert.True(GetWindowRect(new System.Windows.Interop.WindowInteropHelper(manager).Handle, out var bounds));
            var bottom = button.PointToScreen(new Point(0, button.ActualHeight));
            var scale = System.Windows.Media.VisualTreeHelper.GetDpi(manager).DpiScaleY;
            Assert.True(manager.ActualHeight <= 464);
            Assert.InRange((bottom.Y - bounds.Top) / scale, 1, 464);
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(button);
            Assert.Equal(Core.Localization.LocalizationService.Instance["Manager.Save"], peer.GetName());
            manager.MaxHeight = 1032;
            manager.Height = 680;
            manager.UpdateLayout();
            Await(Task.Delay(30));
            Assert.True(manager.ActualHeight >= 600);
            Assert.True(GetWindowRect(new System.Windows.Interop.WindowInteropHelper(manager).Handle, out var resized));
            Assert.True((resized.Bottom - resized.Top) / scale >= 600);
            manager.Close();
        }
        Core.Localization.LocalizationService.Instance.SetCulture("en");
        state.Settings.PanelW = 700;
        state.Settings.PanelH = 680;
        var panel = new SearchPanel();
        typeof(SearchPanel).GetMethod("PositionAndSize", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
        var area = WindowTheming.MonitorWorkAreaDipAt(panel.Left, panel.Top);
        Assert.Equal(area.Height, panel.MaxHeight);
        Assert.Equal(Math.Min(680, area.Height), panel.Height);
        panel.Close();
        var editor = new BodyEditorWindow("JSON", "{\"a\":1}", false, 0);
        var picker = (ComboBox)editor.FindName("CodeFormatBox");
        var host = (ContentControl)editor.FindName("EditorHost");
        Assert.IsType<CodeEditor>(host.Content);
        picker.SelectedIndex = 0;
        Assert.IsType<BodyEditor>(host.Content);
        editor.Close();
        var retryPanel = new SearchPanel { ShowActivated = false, ShowInTaskbar = false };
        retryPanel.SourceInitialized += (_, _) => { retryPanel.Left = -32000; retryPanel.Top = -32000; };
        ((TextBox)retryPanel.FindName("Query")).Text = "retry query";
        var report = typeof(SearchPanel).GetMethod("ReportOutput", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False((bool)report.Invoke(retryPanel, new object?[] { false, null })!);
        Assert.True(retryPanel.IsVisible);
        Assert.Equal("retry query", ((TextBox)retryPanel.FindName("Query")).Text);
        retryPanel.Close();
        var managerForDraft = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
        managerForDraft.SourceInitialized += (_, _) => { managerForDraft.Left = -32000; managerForDraft.Top = -32000; };
        managerForDraft.Show();
        var formatOnly = Assert.Single(state.Categories[0].Snippets);
        managerForDraft.SelectSnippet(formatOnly.Id);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var editing = (Snippet)typeof(ManagerWindow).GetField("_editing", flags)!.GetValue(managerForDraft)!;
        // The enlarged editor supplies its format through the model; no text control changes.
        editing.CodeFormat = "json";
        Assert.True((bool)typeof(ManagerWindow).GetMethod("CommitAndPersistCurrent", flags)!.Invoke(managerForDraft, null)!);
        Assert.Equal("json", Assert.Single(state.Store.LoadAll()[0].Snippets).CodeFormat);
        Assert.Equal("body", Assert.Single(state.Store.LoadAll()[0].Snippets).Body);
        Assert.Equal("json", Assert.Single(state.Search.Search("Entry")).Snippet.CodeFormat);
        managerForDraft.AddSnippet("ClipboardDraft", "clipboard body");
        Assert.True(Capture(managerForDraft, "SilentWhileDraft", "silent body"));
        Assert.DoesNotContain(state.Store.LoadAll().SelectMany(c => c.Snippets), s => s.Name == "ClipboardDraft");
        using (var dialogs = new DialogAnswers("DiscardButton")) managerForDraft.Close();
        Assert.DoesNotContain(state.Store.LoadAll().SelectMany(c => c.Snippets), s => s.Name == "ClipboardDraft");
        Assert.Single(state.Store.LoadAll().SelectMany(c => c.Snippets), s => s.Name == "SilentWhileDraft");
    }

    private static void CheckMove(string root, bool lockDestination)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, lockDestination ? "move-target" : "move-source");
        var store = new Store(state.Settings.DataFolder);
        var sn = new Snippet { Name = "Moved", Body = "survives" };
        store.SaveCategory(new Category { Name = "From", Snippets = new() { sn } });
        store.SaveCategory(new Category { Name = "To" });
        state.ReloadData();
        var manager = new ManagerWindow { ShowActivated = false, ShowInTaskbar = false };
        manager.SourceInitialized += (_, _) => { manager.Left = -32000; manager.Top = -32000; };
        manager.Show();
        manager.SelectSnippet(sn.Id);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cats = (List<Category>)typeof(ManagerWindow).GetField("_cats", flags)!.GetValue(manager)!;
        using (var locked = new FileStream(Path.Combine(store.Root, lockDestination ? "To.json" : "From.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            using (var dialogs = new DialogAnswers("OkButton"))
                typeof(ManagerWindow).GetMethod("MoveSelectedTo", flags)!.Invoke(manager, new object[] { cats[1] });
            if (!lockDestination)
                Assert.Equal(sn.Id, Assert.Single(new Store(store.Root).LoadAll().Single(c => c.Name == "To").Snippets).Id);
        }
        if (lockDestination)
            typeof(ManagerWindow).GetMethod("MoveSelectedTo", flags)!.Invoke(manager, new object[] { cats[1] });
        else Save(manager);
        var saved = store.LoadAll();
        Assert.Empty(saved[0].Snippets);
        Assert.Equal(sn.Id, Assert.Single(saved[1].Snippets).Id);
        Assert.Single(state.Search.Search("Moved"));
        manager.Close();
    }

    private static void CheckTrashDuringCapture(string root)
    {
        var state = AppState.Current;
        state.Settings.DataFolder = Path.Combine(root, "trash-concurrency");
        var store = new Store(state.Settings.DataFolder);
        store.SaveCategory(new Category { Name = "A", Snippets = new() { new() { Name = "Existing", Body = "old" } } });
        var deleted = new TrashEntry { Category = "A", Snippet = new() { Name = "Restored", Body = "recoverable" } };
        store.SaveTrash(new() { deleted });
        state.ReloadData();
        var dialog = new TrashDialog { ShowActivated = false, ShowInTaskbar = false };
        dialog.SourceInitialized += (_, _) => { dialog.Left = -32000; dialog.Top = -32000; };
        dialog.Show();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gate = typeof(Store).GetField("_writeGate", flags)!.GetValue(state.Store)!;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = new Thread(() =>
        {
            lock (gate) { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); }
        }) { IsBackground = true };
        writer.Start();
        Task<Snippet?>? capture = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            capture = state.CaptureClipboardAsync("Captured", "background");
            Assert.True(state.IsLibraryBusy);
            using (var answers = new DialogAnswers("OkButton"))
                typeof(TrashDialog).GetMethod("OnRestore", flags)!.Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
            Assert.Equal(deleted.Snippet.Id, Assert.Single(new Store(store.Root).LoadTrash()).Snippet.Id);
            Assert.Single(new Store(store.Root).LoadAll()[0].Snippets);
        }
        finally
        {
            release.Set();
            Assert.True(writer.Join(TimeSpan.FromSeconds(5)));
            if (capture != null) Await(capture);
        }
        typeof(TrashDialog).GetMethod("OnRestore", flags)!.Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
        Assert.Equal(3, new Store(store.Root).LoadAll()[0].Snippets.Count);
        Assert.Single(state.Search.Search("Captured"));
        Assert.Single(state.Search.Search("Restored"));
        dialog.Close();

        new Store(store.Root).SaveTrash(new() { deleted });
        dialog = new TrashDialog { ShowActivated = false, ShowInTaskbar = false };
        dialog.SourceInitialized += (_, _) => { dialog.Left = -32000; dialog.Top = -32000; };
        dialog.Show();
        var external = new TrashEntry { Category = "A", Snippet = new() { Name = "ExternalTrash" } };
        new Store(store.Root).SaveTrash(new() { deleted, external });
        typeof(TrashDialog).GetMethod("OnDeleteForever", flags)!.Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
        Assert.Equal(external.Snippet.Id, Assert.Single(new Store(store.Root).LoadTrash()).Snippet.Id);
        var other = new Store(Path.Combine(root, "other-trash-library"));
        other.SaveCategory(new Category { Name = "B" });
        state.Settings.DataFolder = other.Root;
        state.ReloadData();
        using (var answers = new DialogAnswers("OkButton"))
            typeof(TrashDialog).GetMethod("OnRestore", flags)!.Invoke(dialog, new object[] { dialog, new RoutedEventArgs() });
        Assert.Empty(new Store(other.Root).LoadAll()[0].Snippets);
        Assert.Equal(external.Snippet.Id, Assert.Single(new Store(store.Root).LoadTrash()).Snippet.Id);
        dialog.Close();
    }

    private sealed class DialogAnswers : IDisposable
    {
        private readonly Queue<string> _buttons;
        private readonly DispatcherTimer _timer;
        private readonly DateTime _deadline = DateTime.UtcNow.AddSeconds(5);
        private bool _timedOut;

        public DialogAnswers(params string[] buttons)
        {
            _buttons = new(buttons);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _timer.Tick += (_, _) =>
            {
                var dialog = Application.Current.Windows.OfType<AppDialog>().LastOrDefault(w => w.IsVisible);
                if (dialog == null) return;
                if (DateTime.UtcNow > _deadline || _buttons.Count == 0)
                {
                    _timedOut = true;
                    dialog.Close();
                    return;
                }
                ((Button)dialog.FindName(_buttons.Dequeue())).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            _timer.Start();
        }

        public void Dispose()
        {
            _timer.Stop();
            Assert.False(_timedOut, "Unexpected manager dialog.");
            Assert.Empty(_buttons);
        }
    }
}
