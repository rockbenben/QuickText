<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · [Français](README.fr.md) · **Deutsch** · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Teil des **[365-Open-Source-Plans](https://github.com/rockbenben/365opensource)** — Projekt Nr. 023 · ein Snippet-Manager und Textexpander im Windows-Infobereich.

**Schluss damit, dasselbe zweimal zu tippen.** QuickText sitzt im Windows-Infobereich: Speichere den Text, den du immer wieder brauchst — E-Mail, Adressen, Signaturen, Vorlagen, Standardantworten, Bilder — einmal ab, und tippe dann in **jedem beliebigen Eingabefeld** ein paar Tasten oder ein Kürzel, und er landet genau an deiner Cursorposition. Mehrzeilig, Sonderzeichen und Emojis bleiben Zeichen für Zeichen erhalten.

> Dein wiederverwendbarer Text, nur ein paar Tastendrücke entfernt — direkt am Cursor abgelegt.

- WPF / .NET 10, portable Einzeldatei-Exe, **kein Konto, standardmäßig offline** — nur die optionale Update-Prüfung kontaktiert GitHub.
- Die Daten liegen als **lokales JSON** in deinem eigenen Ordner — leg ihn in Dropbox / OneDrive / ein NAS, um zu synchronisieren.
- Dunkles Design, **18 Oberflächensprachen** (mit Rechts-nach-links-Spiegelung für Arabisch), Einstellungen greifen sofort.

---

## Wo du es einsetzt

Überall, wo du **unter Windows immer wieder dasselbe tippst**. Es lebt im Infobereich und funktioniert in jedem Eingabefeld (Chat-Fenster, Browser-Formulare, Editoren, Mail-Programme — nicht an eine App gebunden). Es ist **Snippet-Manager und Textbaustein-Tool in einem**, dazu Pinyin-Suche, Variablenvorlagen und Bilder.

| Wer | Was sie speichern |
|---|---|
| **Support / E-Commerce** | Standardantworten, Musterantworten, Werbetexte, QR-Codes oder Produktfotos |
| **Vertrieb / Business** | E-Mail-Vorlagen, Gesprächseinstiege, Angebote, Grußformeln |
| **Entwickler / Ops** | Befehle, Konfiguration, JSON, Boilerplate (`{...}` wird wortwörtlich ausgegeben, nie interpretiert) |
| **Büro / Formulare ausfüllen** | E-Mail, Adresse, Telefon, Ausweisnummern, Vorlagen für Besprechungsnotizen (fragen dich, merken sich den letzten Wert) |
| **HR / Verwaltung / Recht** | Onboarding-Hinweise, Standardbenachrichtigungen, Haftungsausschlüsse — lokal, offline, geeignet für sensible Inhalte |

## In 30 Sekunden erklärt

An **jedem Ort, an dem du tippen kannst** — sagen wir, du brauchst deine E-Mail-Adresse in einem Chat-Fenster:

1. **Aufrufen** — drücke `Ctrl+Shift+8`; das Panel erscheint über dem aktiven Fenster.
2. **Suchen** — tippe ein paar Buchstaben; Treffer werden hervorgehoben. Es durchsucht Name, Pinyin (voll + Initialen), Kürzel und Text.
3. **Enter** — die E-Mail wird **genau dort eingefügt, wo dein Cursor war**. Fertig.

> Tippe nichts, um nach Kategorie zu blättern, mit `Zuletzt` / `Favoriten` oben angeheftet; die am häufigsten genutzten **wandern automatisch nach oben**.
> `↑↓` bewegen · `←→` Kategorie wechseln · `Alt+1–9` Schnellauswahl · Doppelklick zum Senden · `Esc` zum Schließen.

## Drei Wege, es zu erreichen — nimm den passenden

Dieselbe Bibliothek, drei Wege, daraus abzurufen; kombiniere sie frei:

| Weg | Wie ausgelöst | Am besten für |
|---|---|---|
| 🔍 **Panel-Suche** | `Ctrl+Shift+8` → tippen → Enter | Viele Snippets, gelegentliche Nutzung, Auswahl durch Blättern |
| ⌨️ **Inline-Kürzel** | Tippe einfach `;sig`, dann Leertaste / Tab / Enter | Häufige feste Textbausteine, kein Panel nötig |
| 🧩 **Variablenvorlage** | Rufe ein Snippet mit `{variables}` über einen der beiden Wege ab | E-Mail / Formulare: eine Vorlage, ein paar Wörter geändert |

- **Auch Bilder**: aus Zwischenablage oder Datei hinzufügen, beim Senden als Bild eingefügt; Bilder können Kürzel haben, also tippe das Kürzel und erhalte das Bild (QR-Codes, Logo-Signaturen).
- **Ausgabe pro Snippet**: Chat-Textbausteine werden nach dem Einfügen automatisch abgeschickt, Code-Snippets niemals — sie stören nicht.

Alle Details zu Kürzeln und Variablen findest du weiter unten unter **Im Detail** — in den Abschnitten **Platzhalter** und **Kürzel**.

---

# Im Detail

## Funktioniert sofort

Doppelklicke **`QuickText.exe`**; es lebt im **System-Infobereich** (keine Taskleisten-Schaltfläche). Beim **ersten Start**:

- legt es eine kleine **Starter-Bibliothek** an (zwei Kategorien, in deiner **Oberflächensprache**), damit du es sofort ausprobieren kannst;
- zeigt es eine Sprechblase mit dem Aufruf-Hotkey — Standard **`Ctrl+Shift+8`**.

> Die Symbole oben rechts im Panel: **＋ Neu** · **Manager** (den Editor öffnen) · **Einstellungen** · **📌 Anheften** (das Panel nach dem Senden offen halten, um mehrere hintereinander abzufeuern). Spring direkt zum Manager / zu den Einstellungen, ohne über den Infobereich zu gehen.

## Deinen Text hinzufügen / bearbeiten

- **Infobereich → Manager öffnen** — der vollständige Editor: Kategorien links, Snippets rechts, Editor darunter. Kategorien hinzufügen/umbenennen/löschen (mit einer **7-farbigen** Markierung), Snippets bearbeiten (Name, Kürzel, Text, Bild). Zum Umsortieren oder Verschieben zwischen Kategorien ziehen; `Ctrl+Z` macht ein Löschen rückgängig.
- **Infobereich → Neu aus Zwischenablage** — erstellt ein neues Snippet aus dem aktuellen Inhalt der Zwischenablage und öffnet es im Manager zum Fertigstellen (gespeichert, wenn der Manager gespeichert/geschlossen wird).
- **Im Panel erstellen** — tippe den Text ins Suchfeld und drücke `Ctrl+N`, um ihn als neuen Baustein zu speichern (dieser Text wird der Inhalt; `@Kategorie …` legt ihn in dieser Kategorie ab) und zum Fertigstellen in den Manager zu springen (`Ctrl+E` bearbeitet das ausgewählte).

## Platzhalter (eine Vorlage, viele Situationen) · pro Snippet aktivierbar

Platzhalter sind ein **Schalter pro Snippet**: Setze im Manager-Editor den Haken bei „**{variable}-Platzhalter aktivieren**“, und die Tokens unten werden beim Senden aufgelöst; **bleibt der Haken deaktiviert (der Standard), wird der Text wortwörtlich gesendet** — Code, Skripte und JSON voller wörtlicher `{...}` werden nie falsch interpretiert oder abgefragt.

> Beim Upgrade: Platzhalter waren früher immer aktiv. Der erste Start nach dem Upgrade setzt den Haken automatisch für **vorhandene Snippets**, deren Text `{...}` enthält, sodass sich am Verhalten nichts ändert; entferne den Haken im Manager für die, die tatsächlich Code sind.

Wenn aktiviert, werden diese Platzhalter beim Senden aufgelöst:

| Platzhalter | Was er tut |
|---|---|
| `{name}` (beliebige Beschriftung) | **Fordert dich vor dem Einfügen zum Ausfüllen auf**; **merkt sich deinen letzten Wert**, damit Wiederholungen nicht erneut fragen |
| `{name:John}` | Variable mit einem **Standardwert**, im Dialog vorausgefüllt |
| `{env\|dev\|test\|prod}` | Variable mit **Optionen** — der Dialog zeigt ein Dropdown (die erste Option dient zugleich als Standard; freies Tippen bleibt möglich) |
| `{clipboard}` | Fügt den aktuellen Inhalt der Zwischenablage ein |
| `{cursor}` | Lässt den Cursor nach dem Einfügen an dieser Stelle (unterdrückt außerdem das automatische Enter) |
| `{date}` `{time}` `{datetime}` | Fügt das aktuelle Datum/die Uhrzeit ein; unterstützt Verschiebungen wie `{date+7}` (7 Tage in der Zukunft). Chinesische Aliase `{日期}` / `{时间}` / `{日期时间}` funktionieren ebenfalls |
| `{uuid}` `{random}` | Zufallswerte: eine UUID / 6 Ziffern, pro Vorkommen neu |
| `{snippet:name}` | **Bindet den Text eines anderen Snippets ein** (3 Ebenen tief, zyklussicher) — halte gemeinsame Signaturen an einem Ort |

Beispiel: eine Signatur `Best regards,\n{name}` (mit aktivierten Platzhaltern) → fragt beim Senden nach dem Namen → fügt die fertige Signatur ein.

## Kürzel (beim Tippen expandieren, kein Panel) · optional

Gib einem Snippet ein **Kürzel**, um es direkt in jedem Eingabefeld zu expandieren. Das **Auslöse-Präfix** wird einmal in den Einstellungen festgelegt (Standard `;`):

1. Tippe im Manager nur das **Kürzel selbst** — z. B. `sig` für deine Signatur. Das Präfix wird automatisch ergänzt (nicht erneut eingeben); das Feld zeigt das aktuelle Präfix `;` davor.
2. Tippe in einem beliebigen Eingabefeld **`;sig`** (= Präfix + Kürzel), dann **Leertaste / Tab / Enter** → es löscht `;sig` und expandiert den Text (ein `{placeholder}` fragt zuerst nach, wenn bei diesem Snippet Platzhalter aktiviert sind).
3. **Vertippt?** Drücke **direkt danach Rücktaste**, um die Expansion wieder zu `;sig` zurückzusetzen (gleiches Fenster, innerhalb von 5 s).

Details: Der Abgleich ist **ohne Berücksichtigung der Groß-/Kleinschreibung** (`;SIG` löst mit aktivierter Feststelltaste aus); ein mit Rücktaste korrigierter Tippfehler expandiert trotzdem; zwei Snippets mit demselben Kürzel erhalten im Manager eine **Inline-Warnung**; und das Infobereich-Menü hat einen Ein-Klick-Umschalter **„Expansion pausieren“** (für Demos/Spiele).

> **Zum Präfix:** Das Präfix (Standard `;`) verhindert, dass normales Tippen / Pinyin ein bloßes Kürzel fälschlich auslöst. Ändere es unter **Einstellungen → Kürzel → Auslöse-Präfix** (auf `,`, `:`, …) oder **lass es leer**. Ein Kürzel mit Präfix begrenzt sich selbst und löst auch dann aus, wenn es direkt am vorangehenden Text klebt (z. B. `thx;sig`); ohne Präfix löst es nur als eigenständiges Wort aus (nie als Ende eines längeren wie `graf`). Du kannst Kürzel auch pro App deaktivieren (eine Sperrliste, z. B. `cmd.exe; putty.exe`) oder ganz ausschalten.

## Ausgabe & gängige Einstellungen (Infobereich → Einstellungen)

- **Ausgabe** — Standard „in die aktive App einfügen“; oder „nur in die Zwischenablage kopieren“ (du fügst mit `Ctrl+V` ein). Optional: nach dem Einfügen Enter drücken, mit Einfachklick senden, Zwischenablage wiederherstellen. **Jedes Snippet kann dies überschreiben** (Manager → Ausgabe: global folgen / einfügen / einfügen + Enter / nur kopieren) — Chat-Textbausteine werden automatisch gesendet, Code-Snippets niemals.
- **Panel-Position** — dem aktiven Fenster folgen (Standard) / dem Textcursor folgen / letzte Position merken.
- **Aufruf-Methode (eine wählen)** — ① **Tastenkombination**: klicke ins Feld und drücke eine neue (normale Tasten brauchen `Ctrl`/`Alt`/`Shift`/`Win`; Funktionstasten **`F1`–`F24` funktionieren allein**); oder ② **Modifikatortaste tippen**: **einfaches oder doppeltes Tippen einer Modifikatortaste** (z. B. rechte `Ctrl`, rechte `Shift`) zum Aufrufen (eine einzelne Modifikatortaste kann kein normaler Hotkey sein, wird also per Tippen erkannt). Die Wahl des Tippens deaktiviert die Kombination — beide schließen sich gegenseitig aus, sodass immer klar ist, welche aktiv ist.
- **Erfassungs-Hotkey** — optionale zweite Kombination, die **die Zwischenablage still als neues Snippet speichert** (Sprechblasen-Rückmeldung, kein Fenster).
- **Datenordner** — richte ihn auf ein Synchronisierungslaufwerk; **Backup exportieren / importieren** (Zip, mit Überschreib-Bestätigung geprüft); tägliches **Auto-Backup** auf diesem Rechner (die neuesten 10 bleiben, Ein-Klick-Ordnerzugriff).
- **Sprache** — **18 Sprachen**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, sofort umgeschaltet. **Mit Windows starten** optional.
- **Nach Updates suchen** — standardmäßig aus; wenn aktiviert, verbindet sich die App beim Start einmal mit GitHub, um nach einer neueren Version zu suchen (das einzige Mal online). „Jetzt suchen“ führt es bei Bedarf aus.

## Tastatur-Spickzettel

| Aktion | Tasten |
|---|---|
| Panel aufrufen / schließen | `Ctrl+Shift+8` (konfigurierbar) / `Esc` |
| Auswählen / Kategorie wechseln / Schnellauswahl | `↑↓` / `←→` / `Alt+1–9` |
| Senden | `Enter` oder Doppelklick (Einfachklick optional) |
| Favorit / kein Favorit | `Ctrl+D` |
| Im Panel erstellen / bearbeiten | `Ctrl+N` / `Ctrl+E` |
| Löschen rückgängig (Manager) | `Ctrl+Z` |
| Kürzel: auslösen / rückgängig | Kürzel + Leertaste·Tab·Enter tippen / Rücktaste nach dem Expandieren |

---

## Funktionen auf einen Blick

- **Aufrufen**: globaler Hotkey — eine **Tastenkombination** (Funktionstasten funktionieren als Einzeltaste) oder **einfaches/doppeltes Tippen einer Modifikatortaste** (z. B. rechte Ctrl), **eine wählen**; das Panel folgt dem aktiven Fenster / dem Textcursor / der gemerkten Position; die Schaltflächen oben springen zu **Neu / Manager / Einstellungen**; anheften, um mehrere hintereinander zu senden; ziehen & Größe ändern mit gemerkter Größe.
- **Suche**: Name / Pinyin / Initialen / Kürzel / Text, hervorgehoben; **Gleichstände nach Nutzungshäufigkeit (Frecency) aufgelöst**; `@category keywords` grenzt die Suche auf eine Kategorie ein (bloßes `@category` durchblättert sie).
- **Inhalt**: Klartext (mehrzeilig, Sonderzeichen, Emojis verlustfrei), Platzhalter (Standardwerte / Options-Dropdowns / Snippet-Verschachtelung / uuid / random — **pro Snippet aktivierbar**), **Bilder** (aus Zwischenablage oder Datei, beim Senden als Bild eingefügt; **Bilder können ebenfalls Kürzel haben** — tippe das Kürzel, erhalte das Bild).
- **Kürzel**: durch Abschlusszeichen ausgelöst, Variablen-Abfrage, Rückgängig mit einem Tastendruck, Tippfehlerkorrektur per Rücktaste, ohne Groß-/Kleinschreibung, Klick trennt das Token, Duplikat-Warnung, App-Sperrliste, **Ein-Klick-Pause im Infobereich**.
- **Ausgabe**: direkt einfügen / nur kopieren; optional automatisches Enter, Zwischenablage wiederherstellen, Senden per Einfachklick; **Ausgabe-Überschreibung pro Snippet**; Erfassungs-Hotkey (Zwischenablage → Snippet mit einem Tastendruck).
- **Manager**: 7 Kategoriefarben, Ziehen zum Umsortieren / Verschieben, **Mehrfachauswahl für Stapel-Verschieben / -Löschen** (Ctrl / Shift auswählen, dann Rechtsklick), Löschen rückgängig, **Papierkorb (30-Tage-Wiederherstellung, mit Textvorschau)**, Duplikat-Kürzel-Warnung, Nutzungsstatistik, Zeilenumbruch-aus-Modus für Code, Speicher-Rückmeldung.
- **Daten**: lokales JSON, Hot-Reload (führt externe Änderungen / Synchronisierung automatisch zusammen), Hinweis bei Synchronisierungskonflikten, Backup exportieren / importieren, **tägliches Auto-Backup (10 aufbewahrt)**, mit Windows starten.
- **Lokalisierung**: **18 Oberflächensprachen** (vereinfachtes / traditionelles Chinesisch, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) mit **Rechts-nach-links-Spiegelung für Arabisch**, live in den Einstellungen umgeschaltet.
- **Robustheit**: einzelne Instanz (ein zweiter Start ruft das Suchpanel auf, statt Hooks doppelt zu installieren); CI führt bei jedem Push Tests plus einen Fenster-Smoke-Check aus und veröffentlicht bei `v*`-Tags eine Einzeldatei-Exe.

## Daten & Synchronisierung

Datenordner (Standard `Documents\QuickText`, in den Einstellungen änderbar, kann auf ein Synchronisierungslaufwerk zeigen):

```
<data folder>/
  ├─ index.json        # category order + each category's file name and color
  ├─ <category>.json   # the snippets in that category (Snippet[])
  ├─ trash.json        # soft-deleted snippets (auto-purged after 30 days)
  └─ images/           # image files for image snippets
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Atomare Schreibvorgänge** (`*.tmp` → `File.Replace`), damit eine Synchronisierung nie eine halb geschriebene Datei liest; `FileSystemWatcher`-Hot-Reload (gebündelt), mit einem Schutz gegen Selbst-Schreibvorgänge.
- Maschinenlokaler Zustand bleibt **außerhalb des Synchronisierungsordners**: Einstellungen in `%APPDATA%\QuickText\settings.json`, Nutzungszähler / Favoriten in `%APPDATA%\QuickText\usage.stats` (sie ändern sich bei jedem Senden und würden zwischen Rechnern kollidieren), tägliche Auto-Backups in `%APPDATA%\QuickText\backups\`.
- **Portabler Modus** (spurenfrei / USB): Aktiviere ihn unter **Einstellungen → Daten → Portabler Modus** — er legt eine `QuickText.portable`-Markierung neben `QuickText.exe` ab und **greift beim nächsten Neustart** (der erste portable Start überträgt deine Einstellungen und Nutzung mit, sodass du nicht neu konfigurieren musst). Einstellungen, Nutzung, Backups und die Standardbibliothek liegen dann unter `<exe folder>\Data\` statt in `%APPDATA%` / Documents, und „mit Windows starten“ verwendet eine Verknüpfung im Autostart-Ordner statt der Registry — so reist das ganze Tool auf einem Stick und hinterlässt nichts auf dem Host. Die App muss an einem beschreibbaren Ort liegen (ein USB-Stick, nicht `Program Files`); die **Textbibliothek wandert über Backup exportieren / importieren**. „Mit Windows starten“ wird pro Modus verfolgt, also hake es nach dem Wechsel im neuen Modus erneut an, falls du es möchtest. Lass es für das oben beschriebene installierte Layout aus (die richtige Wahl, wenn der Datenordner ein Synchronisierungslaufwerk ist).<br>*(Es muss eine Markierungsdatei sein, keine einfache Einstellung — sie entscheidet, wo `settings.json` selbst liegt; der Schalter greift erst beim nächsten Start und stört nie die laufende Sitzung.)*

## Marke

Die Assets liegen in `assets/branding/`: `quicktext-mark.svg` (primär), `quicktext-mark-mono.svg` (monochrom), `quicktext.ico` (16–256, App-/Infobereich-Symbol), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (Markenblatt). Das Zeichen ist ein **Text-Cursor (I-Beam)** auf einer mintfarbenen Kachel neben einem bernsteinfarbenen Block gerade gelandeten Textes — „text▮“, also dein Text an der Cursorposition. Palette „terminal dusk“: Mint (die App-Akzentfarbe `#3DC2A0`) + Bernstein `#F2B457` auf teal-lastigem Tintenton; eine Sans-Wortmarke gepaart mit der Monospace-Schrift Cascadia Code.

## Architektur

Reiner Core (kein Win32, unit-testbar) getrennt von Win32/UI gehalten.

| Projekt | Inhalt |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 Sprachen) |
| `src/QuickText.App` | WPF-UI (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (dunkles Design), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Core-Unit-Tests (xUnit) |

## Build & Ausführung

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # or run QuickText.exe under bin
```

Einen portablen Einzeldatei-Build veröffentlichen (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Erfordert das .NET 10 SDK. Nur Windows (Win32-Globaler-Hotkey / Tastatur-Hook / Zwischenablage).

## Über den 365-Open-Source-Plan

Dies ist Projekt Nr. 023 des [365-Open-Source-Plans](https://github.com/rockbenben/365opensource).

Eine Person + KI, über 300 Open-Source-Projekte in einem Jahr. [Reiche deine Idee ein →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Lizenz

[MIT License](../LICENSE) · Copyright © 2026 rockbenben. Frei nutzbar, veränderbar und verteilbar.
