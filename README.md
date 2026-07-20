<p align="left">
  <img src="assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

**English** · [简体中文](README.zh.md) · [繁體中文](docs/README.zh-Hant.md) · [日本語](docs/README.ja.md) · [한국어](docs/README.ko.md) · [Español](docs/README.es.md) · [Português](docs/README.pt.md) · [Français](docs/README.fr.md) · [Deutsch](docs/README.de.md) · [Italiano](docs/README.it.md) · [Русский](docs/README.ru.md) · [Tiếng Việt](docs/README.vi.md) · [ไทย](docs/README.th.md) · [Bahasa Indonesia](docs/README.id.md) · [हिन्दी](docs/README.hi.md) · [বাংলা](docs/README.bn.md) · [العربية](docs/README.ar.md) · [Türkçe](docs/README.tr.md)

# QuickText

> Part of the **[365 Open Source Plan](https://github.com/rockbenben/365opensource)** — project #023 · a tray snippet manager & text expander for Windows.

**Stop typing the same thing twice.** QuickText lives in the Windows tray: store the text you reach for again and again — email, addresses, signatures, templates, canned replies, images — once, then in **any input box** type a few keystrokes or an abbreviation and it lands right at your cursor. Multi-line, special characters and emoji preserved character-for-character.

> Your reusable text, a few keystrokes away — dropped straight at the cursor.

- WPF / .NET 10, single-file portable exe, **no account, offline by default** — only the optional update check ever contacts GitHub.
- Data is **local JSON** in your own folder — put it in Dropbox / OneDrive / a NAS to sync.
- Dark theme, **18 UI languages** (with right-to-left mirroring for Arabic), settings apply instantly.

---

## Where you'd use it

Wherever you **type the same thing over and over on Windows**. It lives in the tray and works in every input box (chat windows, browser forms, editors, mail clients — not tied to one app). It's a **snippet manager and text expander in one**, plus pinyin search, variable templates, and images.

| Who                       | What they store                                                                                     |
| ------------------------- | --------------------------------------------------------------------------------------------------- |
| **Support / e-commerce**  | Canned replies, standard answers, promo blurbs, QR codes or product shots                           |
| **Sales / business**      | Email templates, openers, quotes, sign-offs                                                         |
| **Developers / ops**      | Commands, config, JSON, boilerplate (`{...}` emitted verbatim, never parsed)                        |
| **Office / form-filling** | Email, address, phone, ID numbers, meeting-note templates (prompt you, remember the last value)     |
| **HR / admin / legal**    | Onboarding notices, standard notifications, disclaimers — local, offline, fit for sensitive content |

## See it in 30 seconds

In **any place you can type** — say you need your email in a chat box:

1. **Summon** — press `Ctrl+Shift+8`; the panel appears over the active window.
2. **Search** — type a few letters; matches are highlighted. It matches the name, pinyin (full + initials), abbreviation, and body.
3. **Enter** — the email is pasted **right where your cursor was**. Done.

> Type nothing to browse by category, with `Recent` / `Favorites` pinned on top; the ones you use most **float to the top** automatically.
> `↑↓` move · `←→` switch category · `Alt+1–9` quick-pick · double-click to send · `Esc` to close.

## Three ways to reach it — pick what fits

Same library, three ways to pull from it; mix them freely:

| Way                        | How to trigger                                            | Best for                                         |
| -------------------------- | --------------------------------------------------------- | ------------------------------------------------ |
| 🔍 **Panel search**        | `Ctrl+Shift+8` → type → Enter                             | Many snippets, occasional use, browsing to pick  |
| ⌨️ **Inline abbreviation** | Just type `;sig` then Space / Tab / Enter                 | High-frequency fixed phrases, no panel needed    |
| 🧩 **Variable template**   | Pull a snippet with `{variables}` via either of the above | Email / forms: one template, a few words changed |

- **Images too**: add from clipboard or file, pasted as an image on send; images can have abbreviations, so type the abbr and get the picture (QR codes, logo signatures).
- **Per-snippet output**: chat phrases auto-send after paste, code snippets never do — they don't interfere.

Full detail on abbreviations and variables is in **In detail** below — the **Placeholders** and **Abbreviations** sections.

---

# In detail

## Works out of the box

Double-click **`QuickText.exe`**; it lives in the **system tray** (no taskbar button). On **first run** it:

- drops in a small **starter library** (two categories, in your **UI language**) so you can try it immediately;
- shows a balloon with the summon hotkey — default **`Ctrl+Shift+8`**.

> The panel's top-right icons: **＋ New** · **Manager** (open the editor) · **Settings** · **📌 Pin** (keep the panel open after a send, to fire several in a row). Jump straight to the Manager / Settings without going back to the tray.

## Add / edit your text

- **Tray → Open Manager** — the full editor: categories on the left, snippets on the right, editor below. Add/rename/delete categories (with a **7-color** tag), edit snippets (name, abbreviation, body, image). Drag to reorder or move between categories; `Ctrl+Z` undoes a delete.
- **Tray → New from clipboard** — create a new snippet from the current clipboard and open it in the Manager to finish (saved when the Manager is saved/closed).
- **Create in the panel** — type the text in the search box and press `Ctrl+N` to save it as a new snippet (that text becomes the body; `@category …` files it in that category) and jump to the Manager to finish (`Ctrl+E` edits the selected one).

## Placeholders (one template, many situations) · opt-in per snippet

Placeholders are a **per-snippet switch**: tick "**Enable {variable} placeholders**" in the Manager editor and the tokens below are resolved on send; **left unticked (the default), the body is sent verbatim** — code, scripts and JSON full of literal `{...}` are never misparsed or prompted on.

> Upgrading: placeholders used to be always-on. The first launch after upgrading automatically ticks the switch for **existing snippets** whose body contains `{...}`, so nothing changes behavior; untick it in the Manager for the ones that are actually code.

When enabled, these placeholders are resolved on send:

| Placeholder                    | What it does                                                                                                                                                                                                                                                                                                                                                                           |
| ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `{name}` (any label)           | **Prompts you to fill it in** before pasting; **remembers your last value** so repeats don’t re-ask                                                                                                                                                                                                                                                                                    |
| `{name:John}`                  | Variable with a **default**, prefilled in the prompt                                                                                                                                                                                                                                                                                                                                   |
| `{env\|dev\|test\|prod}`       | Variable with **options** — the prompt shows a dropdown (first option doubles as the default; free typing still allowed)                                                                                                                                                                                                                                                               |
| `{clipboard}`                  | Inserts the current clipboard contents                                                                                                                                                                                                                                                                                                                                                 |
| `{cursor}`                     | Leaves the caret at this spot after pasting (also suppresses auto-Enter)                                                                                                                                                                                                                                                                                                               |
| `{date}` `{time}` `{datetime}` | Inserts the current date/time; supports offsets like `{date+7}` (7 days out) and custom formats like `{date=MMM d, yyyy}` / `{time=HH:mm:ss}`, combinable with offsets: `{date+7=MM-dd}`. Formats are WYSIWYG (`:` `/` output verbatim, always Gregorian), and `{date=dddd}` names the weekday in the interface language. Chinese aliases `{日期}` / `{时间}` / `{日期时间}` also work |
| `{uuid}` `{random}`            | Random values: a UUID / 6 digits, fresh per occurrence                                                                                                                                                                                                                                                                                                                                 |
| `{snippet:name}`               | **Inlines another snippet's body** (3 levels deep, cycle-safe) — keep shared signatures in one place                                                                                                                                                                                                                                                                                   |

Example: a signature `Best regards,\n{name}` (with placeholders enabled) → asks for the name on send → pastes the finished signature.

## Abbreviations (expand as you type, no panel) · optional

Give a snippet an **abbreviation** to expand it directly in any input box. The **trigger prefix** is set once in Settings (default `;`):

1. In the Manager, type just the **abbreviation itself** — e.g. `sig` for your signature. The prefix is added automatically (don't retype it); the field shows the current prefix `;` in front.
2. In any input box type **`;sig`** (= prefix + abbreviation) then **Space / Tab / Enter** → it deletes `;sig` and expands the body (a `{placeholder}` prompts first when that snippet has placeholders enabled).
3. **Mistyped?** Press **Backspace right after** the expansion to revert it back to `;sig` (same window, within 5 s).

Details: matching is **case-insensitive** (`;SIG` fires with CapsLock on); a typo corrected with Backspace still expands; two snippets sharing an abbreviation get an **inline warning** in the Manager; and the tray menu has a one-click **“Pause expansion”** toggle (for demos/games).

> **About the prefix:** the prefix (default `;`) keeps ordinary typing / pinyin from false-triggering a bare abbreviation. Change it under **Settings → Abbreviations → Trigger prefix** (to `,`, `:`, …) or **leave it empty**. A prefixed abbreviation is self-delimiting and fires even glued to the preceding text (e.g. `thx;sig`); with no prefix it only fires as its own word (never as the tail of a longer one like `graf`). You can also disable abbreviations per app (a blacklist, e.g. `cmd.exe; putty.exe`) or turn them off entirely.

## Output & common settings (Tray → Settings)

- **Output** — default “paste into the active app”; or “copy to clipboard only” (you paste with `Ctrl+V`). Optional: press Enter after pasting, single-click to send, restore clipboard. **Each snippet can override this** (Manager → Output: follow global / paste / paste + Enter / copy only) — chat phrases auto-send, code snippets never do.
- **Panel position** — follow the active window (default) / follow the text caret / remember last position.
- **Summon method (pick one)** — ① **key combo**: click the box and press a new one (ordinary keys need `Ctrl`/`Alt`/`Shift`/`Win`; function keys **`F1`–`F24` work on their own**); or ② **tap a modifier**: **single- or double-tap one modifier** (e.g. right `Ctrl`, right `Shift`) to summon (a lone modifier can't be a normal hotkey, so it's detected by tap). Choosing tap disables the combo — the two are mutually exclusive, so it's always clear which one is live.
- **Capture hotkey** — optional second combo that **silently saves the clipboard as a new snippet** (balloon feedback, no window).
- **Data folder** — point it at a sync drive; **export / import backup** (zip, validated with an overwrite confirm); daily **auto-backup** to this machine (newest 10 kept, one-click folder access).
- **Language** — **18 languages**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, switched instantly. **Start with Windows** optional.
- **Check for updates** — off by default; when on, the app contacts GitHub once at startup to see if a newer release exists (the only time it goes online). "Check now" runs it on demand.

## Keyboard cheat-sheet

| Action                                | Keys                                                    |
| ------------------------------------- | ------------------------------------------------------- |
| Summon / close panel                  | `Ctrl+Shift+8` (configurable) / `Esc`                   |
| Select / switch category / quick-pick | `↑↓` / `←→` / `Alt+1–9`                                 |
| Send                                  | `Enter` or double-click (single-click optional)         |
| Favorite / unfavorite                 | `Ctrl+D`                                                |
| Create / edit in panel                | `Ctrl+N` / `Ctrl+E`                                     |
| Undo delete (Manager)                 | `Ctrl+Z`                                                |
| Abbreviation: trigger / undo          | type abbr + Space·Tab·Enter / Backspace after expanding |

---

## Features at a glance

- **Summon**: global hotkey — a **key combo** (function keys work as a single key) or **single/double-tap a modifier** (e.g. right Ctrl), **pick one**; panel follows active window / text caret / remembered position; top-row buttons jump to **New / Manager / Settings**; pin it to send several in a row; drag & resize with remembered size.
- **Search**: name / pinyin / initials / abbreviation / body, highlighted; **ties broken by usage frequency (frecency)**; `@category keywords` scopes the search to one category (bare `@category` browses it).
- **Content**: plain text (multi-line, special chars, emoji lossless), placeholders (defaults / option dropdowns / snippet nesting / custom date formats / uuid / random — **opt-in per snippet**), **images** (from clipboard or file, pasted as an image on send; **images can have abbreviations too** — type the abbr, get the image).
- **Abbreviations**: terminator-triggered, variable prompt, one-press undo, Backspace typo-correction, case-insensitive, click breaks the token, duplicate warning, per-app blacklist, **one-click tray pause**.
- **Output**: paste directly / copy only; optional auto-Enter, restore clipboard, single-click send; **per-snippet output override**; capture hotkey (clipboard → snippet in one press).
- **Manager**: 7 category colors, drag reorder / move, **multi-select batch move / delete** (Ctrl / Shift select, then right-click), undo delete, **trash (30-day restore, with body preview)**, duplicate-abbr warning, usage stats, no-wrap mode for code, save feedback.
- **Data**: local JSON, hot-reload (auto-merges external edits / sync), sync-conflict notice, export / import backup, **daily auto-backup (10 kept)**, start with Windows.
- **Localization**: **18 UI languages** (Simplified / Traditional Chinese, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) with **right-to-left mirroring for Arabic**, switched live in Settings.
- **Robustness**: single instance (a second launch summons the search panel instead of double-installing hooks); CI runs tests plus a window smoke-check on every push and publishes a single-file exe on `v*` tags.

## Data & sync

Data folder (default `Documents\QuickText`, changeable in Settings, can point at a sync drive):

```
<data folder>/
  ├─ index.json        # category order + each category's file name and color
  ├─ <category>.json   # the snippets in that category (Snippet[])
  ├─ trash.json        # soft-deleted snippets (auto-purged after 30 days)
  └─ images/           # image files for image snippets
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Atomic writes** (`*.tmp` → `File.Replace`) so a sync never reads a half-written file; `FileSystemWatcher` hot-reload (coalesced), with a self-write guard.
- Machine-local state stays **out of the sync folder**: settings in `%APPDATA%\QuickText\settings.json`, usage counts / favorites in `%APPDATA%\QuickText\usage.stats` (they change on every send and would conflict across machines), daily auto-backups in `%APPDATA%\QuickText\backups\`.
- **Portable mode** (trace-free / USB): turn it on under **Settings → Data → Portable mode** — it drops a `QuickText.portable` marker next to `QuickText.exe` and **applies on the next restart** (the first portable start carries your settings and usage across, so you don't reconfigure). Settings, usage, backups and the default library then live under `<exe folder>\Data\` instead of `%APPDATA%` / Documents, and "start with Windows" uses a Startup-folder shortcut rather than the registry — so the whole tool travels on a stick and leaves nothing on the host. The app must sit in a writable location (a USB stick, not `Program Files`); the **text library moves via Export / Import backup**. Start-with-Windows is tracked per mode, so re-tick it in the new mode after switching if you want it. Leave it off for the installed layout above (the right choice when the data folder is a sync drive).<br>_(It has to be a marker file, not a plain setting — it decides where `settings.json` itself lives; the switch only takes effect next start, never disturbing the current session.)_

## Brand

Assets live in `assets/branding/`: `quicktext-mark.svg` (primary), `quicktext-mark-mono.svg` (monochrome), `quicktext.ico` (16–256, app / tray icon), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (brand sheet). The mark is a **text I-beam caret** on a mint tile beside an amber block of just-landed text — “text▮”, i.e. putting your text at the cursor. Palette “terminal dusk”: mint (the app accent `#3DC2A0`) + amber `#F2B457` on teal-biased ink; a sans wordmark paired with Cascadia Code mono.

## Architecture

Pure Core (no Win32, unit-testable) kept separate from Win32/UI.

| Project                      | Contents                                                                                                                                                                                                             |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/QuickText.Core`         | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 languages)             |
| `src/QuickText.App`          | WPF UI (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (dark theme), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Core unit tests (xUnit)                                                                                                                                                                                              |

## Build & run

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # or run QuickText.exe under bin
```

Publish a single-file portable build (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Requires the .NET 10 SDK. Windows only (Win32 global hotkey / keyboard hook / clipboard).

## About the 365 Open Source Plan

This is project #023 of the [365 Open Source Plan](https://github.com/rockbenben/365opensource).

One person + AI, 300+ open-source projects in a year. [Submit your idea →](https://365.aishort.top/)

## License

[MIT License](LICENSE) · Copyright © 2026 rockbenben. Free to use, modify, and distribute.
