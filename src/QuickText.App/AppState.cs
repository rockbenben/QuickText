using QuickText.Core.Abbr;
using QuickText.Core.Models;
using QuickText.Core.Persistence;
using QuickText.Core.Pinyin;
using QuickText.Core.Search;
using QuickText.Core.Settings;

namespace QuickText.App;

public sealed class AppState
{
    public static AppState Current { get; } = new();

    public SettingsStore SettingsStore { get; } = new(Core.Settings.SettingsStore.DefaultPath);
    public AppSettings Settings { get; set; } = new();
    public Store Store { get; set; } = null!;
    public SearchIndex Search { get; } = new(new CachedPinyinProvider(new ToolGoodPinyinProvider()));
    public AbbrMatcher Abbr { get; } = new();

    /// <summary>Categories with their snippets, for browse-by-category in the search panel.</summary>
    public IReadOnlyList<Category> Categories { get; private set; } = new List<Category>();

    // Assigned by ReloadData (always called during startup); no eager bootstrap load —
    // constructing a store here would read and parse usage.stats only to be discarded.
    public UsageStore Usage { get; private set; } = null!;

    private readonly Dictionary<string, (Snippet Snippet, string Category)> _byId = new();

    /// <summary>Last rail category the user browsed, so the panel reopens where they left off (this session).</summary>
    public string LastCategory { get; set; } = "";

    // Machine-local state root: %APPDATA%\QuickText, or <exeDir>\Data in portable mode.
    private static string MachineStateDir => Core.AppPaths.MachineStateDir;

    /// <summary>Where the daily automatic zips live (Settings has an "open" button for it).</summary>
    public static string BackupDir => Core.AppPaths.BackupDir;

    /// <summary>One-time move of usage.stats out of the data folder into %APPDATA%.</summary>
    private static void MigrateUsageStats(string dataFolder)
    {
        try
        {
            var old = System.IO.Path.Combine(dataFolder, Core.AppPaths.UsageFileName);
            if (!System.IO.File.Exists(old)) return;
            System.IO.Directory.CreateDirectory(MachineStateDir);
            var dest = System.IO.Path.Combine(MachineStateDir, Core.AppPaths.UsageFileName);
            if (!System.IO.File.Exists(dest)) System.IO.File.Move(old, dest);
            else System.IO.File.Delete(old);
        }
        catch { }
    }

    public string ResolveDataFolder()
    {
        if (!string.IsNullOrWhiteSpace(Settings.DataFolder)) return Settings.DataFolder;
        // Documents\QuickText normally; <exeDir>\Data\library when running portable.
        var def = Core.AppPaths.DefaultDataFolder;
        // Best-effort create. If the default location is unavailable (redirected/offline
        // Documents), do NOT throw here — this runs on the startup path and from StartWatching
        // outside their guards; return the path and let the actual read/write fail where it's
        // already handled (startup falls back to InitEmpty; saves surface a "save failed" error).
        try { System.IO.Directory.CreateDirectory(def); } catch { }
        return def;
    }

    /// <summary>
    /// Fallback when the configured data folder can't be reached at startup (unplugged USB /
    /// offline share / deleted): set up empty in-memory state so the tray + hotkey still come up
    /// instead of throwing before they exist (which would leave a hidden zombie process). The
    /// Store points at the unreachable folder — saves fail gracefully with a message — and the
    /// user can re-point it in Settings or replug and relaunch.
    /// </summary>
    public void InitEmpty()
    {
        var folder = ResolveDataFolder();   // never throws (best-effort create)
        Store = new Store(folder);
        Usage?.Flush();
        try { Usage = new UsageStore(MachineStateDir); } catch { Usage = new UsageStore(System.IO.Path.GetTempPath()); }
        RebuildIndexes(new List<Category>());
    }

    /// <summary>On a fresh install (no library yet), seed a small starter set so the tool is usable immediately.</summary>
    public void SeedStarterLibraryIfEmpty()
    {
        var store = new Store(ResolveDataFolder());
        if (store.HasLibrary) return;

        // Seed in the user's UI language (SetCulture already ran at startup); non-CJK locales fall
        // back to English. The STRUCTURE — which snippets, their category, and which bodies use
        // {variables} — lives HERE, once; only the translated strings vary per language (in
        // SeedContentFor). Abbreviations are stored bare; the trigger prefix (default ";") is
        // prepended when matching, so they fire as ";yx", ";em", … The meeting body uses a plain
        // {开始时间} variable — NOT the built-in {时间}, which would auto-fill the current clock time
        // instead of asking for the meeting's start.
        var t = SeedContentFor(SeedLang(Core.Localization.LocalizationService.Instance.Culture));

        Snippet Snip(string key, bool vars = false)
        {
            var (name, abbr, body) = t.Texts[key];
            return new Snippet { Name = name, Abbr = abbr, Body = body, UseVariables = vars };
        }
        Category Cat(string name, params Snippet[] snips)
        {
            var cat = new Category { Name = name };
            foreach (var s in snips) cat.Snippets.Add(s);
            return cat;
        }

        store.SaveCategory(Cat(t.Common,
            Snip("email"),
            Snip("greeting",  vars: true),
            Snip("signature", vars: true)));
        store.SaveCategory(Cat(t.Templates,
            Snip("meeting", vars: true),
            Snip("reply")));
    }

    /// <summary>Which of the four seeded languages fits a UI culture (non-CJK → English).</summary>
    private static string SeedLang(System.Globalization.CultureInfo c)
    {
        if (c.TwoLetterISOLanguageName == "ja") return "ja";
        if (c.TwoLetterISOLanguageName != "zh") return "en";
        // Traditional Chinese: modern zh-Hant / zh-Hant-*, region-only zh-TW/HK/MO, legacy zh-CHT;
        // every other zh culture is Simplified.
        var n = c.Name;
        bool traditional = n.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
            || n.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase);
        return traditional ? "zh-Hant" : "zh-Hans";
    }

    /// <summary>Category display names + each starter snippet's (name, abbr, body), keyed by the
    /// structural keys used in <see cref="SeedStarterLibraryIfEmpty"/> (email / greeting / signature
    /// / meeting / reply). One flat table per language — add a snippet key here for every language.</summary>
    private sealed record SeedContent(
        string Common, string Templates,
        Dictionary<string, (string Name, string Abbr, string Body)> Texts);

    private static SeedContent SeedContentFor(string lang)
    {
        const string email = "your.name@example.com";
        return lang switch
        {
            "en" => new("Common", "Templates", new()
            {
                ["email"]     = ("Email",            "em",  email),
                ["greeting"]  = ("Greeting",         "hi",  "Hi {name}, "),
                ["signature"] = ("Signature",        "sig", "Best regards,\n{name}"),
                ["meeting"]   = ("Meeting reminder", "mtg", "Reminder: {meeting} starts at {start time}. Please join on time."),
                ["reply"]     = ("Reply later",      "re",  "Got it — let me take a look and get back to you shortly."),
            }),
            "ja" => new("よく使う", "テンプレート", new()
            {
                ["email"]     = ("メール",         "ml",  email),
                ["greeting"]  = ("あいさつ",       "ai",  "こんにちは、{名前}さん。"),
                ["signature"] = ("署名",           "sig", "よろしくお願いいたします。\n{名前}"),
                ["meeting"]   = ("会議リマインド", "mtg", "リマインド：{会議}は{開始時間}に始まります。時間どおりにご参加ください。"),
                ["reply"]     = ("後で返信",       "re",  "承知しました。確認して、のちほど返信します。"),
            }),
            "zh-Hant" => new("常用", "範本", new()
            {
                ["email"]     = ("郵箱",     "yx", email),
                ["greeting"]  = ("打招呼",   "nh", "你好 {暱稱}，"),
                ["signature"] = ("簽名",     "qm", "此致\n敬禮\n{姓名}"),
                ["meeting"]   = ("會議提醒", "hy", "提醒：{會議} 將於 {開始時間} 開始，請準時參加。"),
                ["reply"]     = ("稍後回覆", "sh", "收到，我看一下，稍後回覆你~"),
            }),
            _ => new("常用", "模板", new()   // zh-Hans (the app's primary language)
            {
                ["email"]     = ("邮箱",     "yx", email),
                ["greeting"]  = ("打招呼",   "nh", "你好 {昵称}，"),
                ["signature"] = ("签名",     "qm", "此致\n敬礼\n{姓名}"),
                ["meeting"]   = ("会议提醒", "hy", "提醒：{会议} 将于 {开始时间} 开始，请准时参加。"),
                ["reply"]     = ("稍后回复", "sh", "收到，我看一下，稍后回复你~"),
            }),
        };
    }

    /// <summary>
    /// One-time upgrade: placeholder processing used to be always-on; preserve that behavior
    /// for pre-existing libraries by opting in every snippet whose body contains a {…} token
    /// (exactly the bodies the legacy pipeline would have acted on). New snippets stay opt-in.
    /// </summary>
    public void MigrateVarsOptInOnce()
    {
        if (Settings.VarsOptInMigrated) return;
        try
        {
            var store = new Store(ResolveDataFolder());
            if (store.HasLibrary)
            {
                foreach (var cat in store.LoadAll())
                {
                    bool changed = false;
                    foreach (var s in cat.Snippets)
                        if (!s.UseVariables && !s.IsImage && Core.Snippets.Placeholders.HasAnyToken(s.Body))
                        {
                            s.UseVariables = true;
                            changed = true;
                        }
                    if (changed) { MarkSelfWrite(); store.SaveCategory(cat); }
                }
            }
            Settings.VarsOptInMigrated = true;
            SettingsStore.Save(Settings);
        }
        catch { /* unreadable folder: leave the flag unset so the next launch retries */ }
    }

    /// <summary>
    /// Silent daily safety net alongside the manual export: zip the data folder into
    /// %APPDATA%\QuickText\backups, keep the newest 10. Call from a background thread.
    /// </summary>
    public void AutoBackupIfDue()
    {
        try
        {
            var dataFolder = ResolveDataFolder();
            var backupDir = BackupDir;
            // Guard against a data folder that CONTAINS the backup dir (recursive zip).
            if (backupDir.StartsWith(dataFolder, StringComparison.OrdinalIgnoreCase)) return;
            if (!System.IO.File.Exists(System.IO.Path.Combine(dataFolder, "index.json"))) return;

            System.IO.Directory.CreateDirectory(backupDir);
            var existing = System.IO.Directory.GetFiles(backupDir, "qt-*.zip")
                .OrderBy(f => f, StringComparer.Ordinal).ToList();   // names sort chronologically
            if (existing.Count > 0
                && DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(existing[^1]) < TimeSpan.FromHours(20))
                return;   // already backed up today

            var name = $"qt-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            var tmp = System.IO.Path.Combine(backupDir, name + ".tmp");
            System.IO.Compression.ZipFile.CreateFromDirectory(dataFolder, tmp);
            System.IO.File.Move(tmp, System.IO.Path.Combine(backupDir, name));

            existing.Add(System.IO.Path.Combine(backupDir, name));
            foreach (var old in existing.SkipLast(10))
                try { System.IO.File.Delete(old); } catch { }
        }
        catch { /* a failed backup must never disturb the app */ }
    }

    /// <summary>Read the whole library from disk (blocking I/O) and apply it. Use off the UI
    /// thread for external/watcher-driven reloads; see <see cref="ScheduleReload"/>.</summary>
    public void ReloadData()
    {
        var folder = ResolveDataFolder();
        var cats = new Store(folder).LoadAll();   // disk read + JSON parse
        ApplyLoaded(folder, cats);
    }

    // Swap in a library that was just read from disk: point the Store at the folder, refresh
    // the machine-local usage store, then rebuild the in-memory indexes.
    private void ApplyLoaded(string folder, List<Category> cats)
    {
        Store = new Store(folder);
        Usage?.Flush();   // don't lose a debounced write when swapping stores (null on first load)
        // Usage counts are machine-local state: keep them OUT of the (possibly synced)
        // data folder — the file changes on every send and would conflict across machines.
        MigrateUsageStats(folder);
        Usage = new UsageStore(MachineStateDir);
        RebuildIndexes(cats);
    }

    /// <summary>
    /// Rebuild search / abbreviation / lookup indexes from a category list already held in
    /// memory — no disk read. The Manager calls this after saving so a save costs one write,
    /// not write + a full-library read-back (the old ReloadData did the latter and stalled the
    /// UI badly on sync drives). Store and Usage are left untouched (unchanged by an edit).
    /// The categories are DEEP-COPIED: the Manager keeps mutating its own Snippet instances as
    /// the user edits, and sharing them would leak unsaved edits into the panel / search index.
    /// </summary>
    public void ApplyCategories(IReadOnlyList<Category> cats) => RebuildIndexes(DeepCopy(cats));

    private static List<Category> DeepCopy(IReadOnlyList<Category> cats) =>
        cats.Select(c => new Category
        {
            Name = c.Name,
            Color = c.Color,
            Snippets = c.Snippets.Select(s => new Snippet
            {
                Id = s.Id, Name = s.Name, Abbr = s.Abbr, Body = s.Body,
                UseVariables = s.UseVariables, OutputMode = s.OutputMode,
                ImagePath = s.ImagePath, UpdatedAt = s.UpdatedAt, CodeFormat = s.CodeFormat
            }).ToList()
        }).ToList();

    private void RebuildIndexes(IReadOnlyList<Category> cats)
    {
        // Snapshot the membership so later in-memory edits in the Manager don't leak into the
        // search index before they're saved (each apply gets its own list, as reloads did).
        Categories = cats.ToList();
        _byId.Clear();
        foreach (var c in cats)
            foreach (var s in c.Snippets)
                _byId[s.Id] = (s, c.Name);
        // Rank equal-quality matches by how often the user actually sends them.
        // The lambda reads the Usage property so it always sees the current store.
        Search.UsageOf = id => Usage.CountOf(id);
        Search.Build(cats);
        // Image snippets expand too: the hook deletes the typed abbr and pastes the image.
        Abbr.Rebuild(cats.SelectMany(c => c.Snippets), Settings.AbbrPrefix);
        // Collisions are silent last-wins in the matcher (case-insensitive; images count
        // too since they became expandable) — record them so startup can warn the user.
        AbbrConflicts = cats.SelectMany(c => c.Snippets)
            .Where(s => !string.IsNullOrEmpty(s.Abbr))
            .GroupBy(s => s.Abbr, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
    }

    private System.IO.FileSystemWatcher? _watcher;
    private System.Windows.Threading.DispatcherTimer? _reloadDebounce;
    private int _reloadGen;    // UI-thread only: monotonic tag assigned per scheduled reload
    private int _appliedGen;   // UI-thread only: highest reload generation actually applied
    public event Action? DataReloaded;
    private DateTime _selfWriteUntil;

    public void MarkSelfWrite() => _selfWriteUntil = DateTime.UtcNow.AddMilliseconds(800);

    public string CategoryOf(string id) => _byId.TryGetValue(id, out var v) ? v.Category : "";

    /// <summary>
    /// {片段:名称} lookup: exact snippet name (case-insensitive); text snippets only.
    /// UseVariables travels along so an opt-out body can be inlined verbatim.
    /// </summary>
    public (string Body, bool UseVariables)? SnippetForNesting(string name)
    {
        var s = _byId.Values
            .Select(v => v.Snippet)
            .FirstOrDefault(x => !x.IsImage && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        return s == null ? null : (s.Body, s.UseVariables);
    }

    /// <summary>Abbreviations claimed by more than one snippet (the matcher keeps only one) — surfaced at startup.</summary>
    public IReadOnlyList<string> AbbrConflicts { get; private set; } = Array.Empty<string>();

    public string ResolveImagePath(string relativePath) =>
        System.IO.Path.Combine(ResolveDataFolder(), relativePath);

    public void RecordUse(string id) => Usage.Record(id);

    /// <summary>Most-used-then-recent snippets across all categories.</summary>
    public List<Snippet> Recents(int n) =>
        Usage.TopIds(n)
            .Where(_byId.ContainsKey)
            .Select(id => _byId[id].Snippet)
            .ToList();

    public bool IsFavorite(string id) => Usage.IsFavorite(id);
    public void ToggleFavorite(string id) => Usage.ToggleFavorite(id);

    public List<Snippet> Favorites(int max) =>
        Usage.FavoriteIds
            .Where(_byId.ContainsKey)
            .Select(id => _byId[id].Snippet)
            .Take(max)
            .ToList();

    public void StartWatching()
    {
        var folder = ResolveDataFolder();
        _watcher?.Dispose();
        _watcher = null;
        // An unreachable/absent data folder (offline share, unplugged USB) makes the
        // FileSystemWatcher ctor throw — don't let that take down startup; live reload simply
        // stays off until the folder is present again (re-armed on the next ReapplySettings).
        try
        {
            var w = new System.IO.FileSystemWatcher(folder, "*.json")
                { NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName, EnableRaisingEvents = true };
            System.IO.FileSystemEventHandler handler = (_, _) =>
            {
                if (DateTime.UtcNow < _selfWriteUntil) return;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(ScheduleReload));
            };
            w.Changed += handler; w.Created += handler;
            w.Deleted += handler; w.Renamed += (_, _) => handler(this, null!);
            _watcher = w;
        }
        catch { /* folder not present: no live reload until it is */ }
    }

    // A sync client (OneDrive/Dropbox/…) usually drops several files in a burst; coalesce the
    // resulting watcher events into a single reload instead of doing a full rebuild per file.
    private void ScheduleReload()
    {
        if (_reloadDebounce == null)
        {
            _reloadDebounce = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(250) };
            _reloadDebounce.Tick += (_, _) =>
            {
                _reloadDebounce!.Stop();
                // The change came from OUTSIDE (a sync client or another editor). Do the
                // blocking library read on a background thread — on a slow sync drive a
                // UI-thread read here froze the app — then apply on the UI thread.
                var folder = ResolveDataFolder();
                // Two bursts can launch two overlapping reads that finish out of order; tag each
                // with a generation and apply a read only if it's newer than the last one applied.
                // Guarding on last-APPLIED (not last-scheduled) means a newer read that FAILS its
                // disk load doesn't strand an older read that succeeded.
                int gen = ++_reloadGen;
                System.Threading.Tasks.Task.Run(() =>
                {
                    List<Category> cats;
                    try { cats = new Store(folder).LoadAll(); }
                    catch { return; }   // mid-write by the sync client: let the next event retry
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (gen <= _appliedGen) return;   // an equal-or-newer reload already applied
                        _appliedGen = gen;
                        ApplyLoaded(folder, cats);
                        DataReloaded?.Invoke();
                    }));
                });
            };
        }
        _reloadDebounce.Stop();
        _reloadDebounce.Start();   // restart on each event → fires once, 250ms after the last
    }
}
