<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · **Italiano** · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Parte del **[Piano 365 open source](https://github.com/rockbenben/365opensource)** — progetto n. 023 · un gestore di snippet ed espansore di testo nella barra di Windows.

**Basta digitare due volte la stessa cosa.** QuickText vive nella barra delle applicazioni di Windows: memorizza una volta il testo a cui ricorri di continuo — email, indirizzi, firme, modelli, risposte preconfezionate, immagini — e poi, in **qualsiasi casella di testo**, digiti pochi tasti o un'abbreviazione e il testo compare direttamente al cursore. Testo su più righe, caratteri speciali ed emoji conservati carattere per carattere.

> Il tuo testo riutilizzabile, a pochi tasti di distanza — inserito direttamente al cursore.

- WPF / .NET 10, exe portatile a file singolo, **nessun account, offline per impostazione predefinita** — solo il controllo aggiornamenti opzionale contatta GitHub.
- I dati sono in **JSON locale** nella tua cartella — mettila su Dropbox / OneDrive / un NAS per sincronizzarli.
- Tema scuro, **18 lingue dell'interfaccia** (con rispecchiamento da destra a sinistra per l'arabo), le impostazioni si applicano all'istante.

---

## Dove ti serve

Ovunque tu **digiti la stessa cosa più e più volte su Windows**. Vive nella barra delle applicazioni e funziona in ogni casella di testo (finestre di chat, moduli del browser, editor, client di posta — non è legato a una sola app). È un **gestore di snippet ed espansore di testo in uno**, più ricerca in pinyin, modelli con variabili e immagini.

| Chi | Cosa memorizza |
|---|---|
| **Assistenza / e-commerce** | Risposte preconfezionate, risposte standard, testi promozionali, codici QR o foto di prodotto |
| **Vendite / business** | Modelli di email, aperture, preventivi, saluti finali |
| **Sviluppatori / ops** | Comandi, configurazioni, JSON, boilerplate (`{...}` emesso alla lettera, mai interpretato) |
| **Ufficio / compilazione moduli** | Email, indirizzo, telefono, numeri di documento, modelli di verbali (te lo chiedono, ricordano l'ultimo valore) |
| **HR / amministrazione / legale** | Comunicazioni di onboarding, notifiche standard, disclaimer — locale, offline, adatto a contenuti sensibili |

## Guardalo in 30 secondi

In **qualsiasi punto in cui puoi digitare** — poniamo che ti serva la tua email in una casella di chat:

1. **Richiama** — premi `Ctrl+Shift+8`; il pannello appare sopra la finestra attiva.
2. **Cerca** — digita qualche lettera; le corrispondenze vengono evidenziate. Cerca nel nome, nel pinyin (completo + iniziali), nell'abbreviazione e nel corpo.
3. **Invio** — l'email viene incollata **esattamente dove si trovava il cursore**. Fatto.

> Non digitare nulla per sfogliare per categoria, con `Recenti` / `Preferiti` fissati in alto; quelli che usi di più **salgono automaticamente** in cima.
> `↑↓` per spostarti · `←→` per cambiare categoria · `Alt+1–9` per la scelta rapida · doppio clic per inviare · `Esc` per chiudere.

## Tre modi per raggiungerlo — scegli quello che fa per te

Stessa libreria, tre modi per attingervi; combinali liberamente:

| Modo | Come attivarlo | Ideale per |
|---|---|---|
| 🔍 **Ricerca nel pannello** | `Ctrl+Shift+8` → digita → Invio | Molti snippet, uso occasionale, sfogliare per scegliere |
| ⌨️ **Abbreviazione inline** | Digita semplicemente `;sig` poi Spazio / Tab / Invio | Frasi fisse ad alta frequenza, senza bisogno del pannello |
| 🧩 **Modello con variabili** | Richiama uno snippet con `{variabili}` tramite uno dei metodi sopra | Email / moduli: un modello, poche parole cambiate |

- **Anche immagini**: aggiungile dagli appunti o da file, incollate come immagine all'invio; le immagini possono avere abbreviazioni, quindi digiti l'abbreviazione e ottieni la figura (codici QR, firme con logo).
- **Output per singolo snippet**: le frasi di chat si inviano automaticamente dopo l'incolla, gli snippet di codice mai — così non interferiscono.

Tutti i dettagli su abbreviazioni e variabili sono in **Nel dettaglio** qui sotto — le sezioni **Segnaposto** e **Abbreviazioni**.

---

# Nel dettaglio

## Funziona da subito

Fai doppio clic su **`QuickText.exe`**; vive nella **barra delle applicazioni** (nessun pulsante sulla barra delle applicazioni). Al **primo avvio**:

- inserisce una piccola **libreria iniziale** (due categorie, nella tua **lingua dell'interfaccia**) per provarlo subito;
- mostra un fumetto con la scorciatoia di richiamo — predefinita **`Ctrl+Shift+8`**.

> Le icone in alto a destra del pannello: **＋ Nuovo** · **Gestore** (apre l'editor) · **Impostazioni** · **📌 Fissa** (mantiene il pannello aperto dopo un invio, per lanciarne diversi di fila). Vai direttamente al Gestore / alle Impostazioni senza tornare alla barra delle applicazioni.

## Aggiungi / modifica il tuo testo

- **Barra delle applicazioni → Apri Gestore** — l'editor completo: categorie a sinistra, snippet a destra, editor in basso. Aggiungi/rinomina/elimina categorie (con un'etichetta a **7 colori**), modifica gli snippet (nome, abbreviazione, corpo, immagine). Trascina per riordinare o spostare tra categorie; `Ctrl+Z` annulla un'eliminazione.
- **Barra delle applicazioni → Nuovo dagli appunti** — crea un nuovo snippet dal contenuto attuale degli appunti e lo apre nel Gestore per completarlo (salvato quando il Gestore viene salvato/chiuso).
- **Crea nel pannello** — digita il testo nella casella di ricerca e premi `Ctrl+N` per salvarlo come nuovo snippet (quel testo diventa il corpo; `@categoria …` lo archivia in quella categoria) e passare al Gestore per completarlo (`Ctrl+E` modifica quello selezionato).

## Segnaposto (un modello, molte situazioni) · attivabili per singolo snippet

I segnaposto sono un **interruttore per singolo snippet**: spunta "**Abilita segnaposto {variabile}**" nell'editor del Gestore e i token qui sotto vengono risolti all'invio; **lasciato non spuntato (l'impostazione predefinita), il corpo viene inviato alla lettera** — codice, script e JSON pieni di `{...}` letterali non vengono mai interpretati per errore né generano richieste.

> In aggiornamento: i segnaposto erano sempre attivi. Il primo avvio dopo l'aggiornamento spunta automaticamente l'interruttore per gli **snippet esistenti** il cui corpo contiene `{...}`, così nulla cambia comportamento; togli la spunta nel Gestore per quelli che sono effettivamente codice.
> Inoltre: `date` / `time` / `datetime` / `now` / `日期` / `时间` / `日期时间` sono ora **nomi riservati di data** — `{date:xyz}` viene letto come formato di data, non più come "una variabile chiamata date con valore predefinito xyz"; rinomina le variabili che usavano questi nomi.

Quando abilitati, questi segnaposto vengono risolti all'invio:

| Segnaposto | Cosa fa |
|---|---|
| `{name}` (qualsiasi etichetta) | **Ti chiede di compilarlo** prima di incollare; **ricorda il tuo ultimo valore** così le ripetizioni non lo richiedono di nuovo |
| `{name:John}` | Variabile con un **valore predefinito**, precompilato nella richiesta |
| `{env\|dev\|test\|prod}` | Variabile con **opzioni** — la richiesta mostra un menu a discesa (la prima opzione funge anche da predefinita; la digitazione libera è comunque consentita) |
| `{clipboard}` | Inserisce il contenuto attuale degli appunti |
| `{cursor}` | Lascia il cursore in questo punto dopo l'incolla (elimina anche l'Invio automatico) |
| `{date}` `{time}` `{datetime}` | Inserisce la data/ora attuale; supporta scostamenti come `{date+7}` (7 giorni dopo) e formati personalizzati come `{date:yyyy-MM-dd}` / `{time:HH:mm:ss}`, combinabili con gli scostamenti: `{date+7:MM-dd}`. Funzionano anche gli alias cinesi `{日期}` / `{时间}` / `{日期时间}` |
| `{uuid}` `{random}` | Valori casuali: un UUID / 6 cifre, nuovi a ogni occorrenza |
| `{snippet:name}` | **Incorpora inline il corpo di un altro snippet** (fino a 3 livelli di profondità, a prova di ciclo) — tieni le firme condivise in un unico posto |

Esempio: una firma `Best regards,\n{name}` (con i segnaposto abilitati) → chiede il nome all'invio → incolla la firma completa.

## Abbreviazioni (si espandono mentre digiti, nessun pannello) · opzionale

Assegna a uno snippet un'**abbreviazione** per espanderlo direttamente in qualsiasi casella di testo. Il **prefisso di attivazione** si imposta una volta nelle Impostazioni (predefinito `;`):

1. Nel Gestore, digita solo l'**abbreviazione vera e propria** — ad es. `sig` per la tua firma. Il prefisso viene aggiunto automaticamente (non ridigitarlo); il campo mostra davanti il prefisso attuale `;`.
2. In qualsiasi casella di testo digita **`;sig`** (= prefisso + abbreviazione) poi **Spazio / Tab / Invio** → elimina `;sig` ed espande il corpo (un `{segnaposto}` chiede prima quando quello snippet ha i segnaposto abilitati).
3. **Errore di battitura?** Premi **Backspace subito dopo** l'espansione per riportarla a `;sig` (stessa finestra, entro 5 s).

Dettagli: la corrispondenza è **senza distinzione tra maiuscole e minuscole** (`;SIG` si attiva con BlocMaiusc attivo); un errore di battitura corretto con Backspace si espande comunque; due snippet che condividono un'abbreviazione ricevono un **avviso inline** nel Gestore; e il menu della barra delle applicazioni ha un interruttore a un clic **"Sospendi espansione"** (per demo/giochi).

> **A proposito del prefisso:** il prefisso (predefinito `;`) evita che la digitazione ordinaria / il pinyin attivino per errore un'abbreviazione nuda. Cambialo in **Impostazioni → Abbreviazioni → Prefisso di attivazione** (in `,`, `:`, …) oppure **lascialo vuoto**. Un'abbreviazione con prefisso è auto-delimitante e si attiva anche se attaccata al testo precedente (es. `thx;sig`); senza prefisso si attiva solo come parola a sé (mai come coda di una più lunga come `graf`). Puoi anche disabilitare le abbreviazioni per singola app (una lista di esclusione, es. `cmd.exe; putty.exe`) o disattivarle del tutto.

## Output e impostazioni comuni (Barra delle applicazioni → Impostazioni)

- **Output** — predefinito "incolla nell'app attiva"; oppure "copia solo negli appunti" (incolli tu con `Ctrl+V`). Opzionale: premere Invio dopo l'incolla, invio con un solo clic, ripristino degli appunti. **Ogni snippet può sovrascriverlo** (Gestore → Output: segui globale / incolla / incolla + Invio / solo copia) — le frasi di chat si inviano automaticamente, gli snippet di codice mai.
- **Posizione del pannello** — segui la finestra attiva (predefinito) / segui il cursore di testo / ricorda l'ultima posizione.
- **Metodo di richiamo (scegline uno)** — ① **combinazione di tasti**: clicca la casella e premi una nuova combinazione (i tasti normali richiedono `Ctrl`/`Alt`/`Shift`/`Win`; i tasti funzione **`F1`–`F24` funzionano da soli**); oppure ② **tocca un modificatore**: **tocco singolo o doppio di un modificatore** (es. `Ctrl` destro, `Shift` destro) per richiamare (un modificatore da solo non può essere una normale scorciatoia, quindi viene rilevato tramite tocco). Scegliendo il tocco si disabilita la combinazione — le due si escludono a vicenda, così è sempre chiaro quale è attiva.
- **Scorciatoia di cattura** — seconda combinazione opzionale che **salva silenziosamente gli appunti come nuovo snippet** (riscontro tramite fumetto, nessuna finestra).
- **Cartella dati** — puntala su un'unità di sincronizzazione; **esporta / importa backup** (zip, convalidato con conferma di sovrascrittura); **backup automatico** giornaliero su questo computer (mantenuti i 10 più recenti, accesso alla cartella con un clic).
- **Lingua** — **18 lingue**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, cambiate all'istante. **Avvio con Windows** opzionale.
- **Controlla aggiornamenti** — disattivato per impostazione predefinita; se attivo, si connette a GitHub una volta all'avvio per verificare la presenza di una nuova versione (l'unica volta in cui va online). «Controlla ora» lo esegue su richiesta.

## Riepilogo scorciatoie da tastiera

| Azione | Tasti |
|---|---|
| Richiama / chiudi pannello | `Ctrl+Shift+8` (configurabile) / `Esc` |
| Seleziona / cambia categoria / scelta rapida | `↑↓` / `←→` / `Alt+1–9` |
| Invia | `Enter` o doppio clic (singolo clic opzionale) |
| Aggiungi / rimuovi dai preferiti | `Ctrl+D` |
| Crea / modifica nel pannello | `Ctrl+N` / `Ctrl+E` |
| Annulla eliminazione (Gestore) | `Ctrl+Z` |
| Abbreviazione: attiva / annulla | digita abbr + Spazio·Tab·Invio / Backspace dopo l'espansione |

---

## Funzionalità in breve

- **Richiamo**: scorciatoia globale — una **combinazione di tasti** (i tasti funzione funzionano come singolo tasto) o **tocco singolo/doppio di un modificatore** (es. Ctrl destro), **scegline uno**; il pannello segue la finestra attiva / il cursore di testo / la posizione memorizzata; i pulsanti della riga superiore vanno a **Nuovo / Gestore / Impostazioni**; fissalo per inviarne diversi di fila; trascina e ridimensiona con dimensione memorizzata.
- **Ricerca**: nome / pinyin / iniziali / abbreviazione / corpo, evidenziati; **parità risolta dalla frequenza d'uso (frecency)**; `@categoria parole chiave` restringe la ricerca a una categoria (il solo `@categoria` la sfoglia).
- **Contenuto**: testo semplice (multiriga, caratteri speciali, emoji senza perdite), segnaposto (valori predefiniti / menu a discesa di opzioni / annidamento di snippet / formati data personalizzati / uuid / random — **attivabili per singolo snippet**), **immagini** (dagli appunti o da file, incollate come immagine all'invio; **anche le immagini possono avere abbreviazioni** — digita l'abbreviazione, ottieni l'immagine).
- **Abbreviazioni**: attivazione con terminatore, richiesta variabili, annullamento con una pressione, correzione errori con Backspace, senza distinzione maiuscole/minuscole, il clic interrompe il token, avviso di duplicati, lista di esclusione per app, **sospensione a un clic dalla barra delle applicazioni**.
- **Output**: incolla direttamente / solo copia; Invio automatico opzionale, ripristino appunti, invio con singolo clic; **sovrascrittura output per singolo snippet**; scorciatoia di cattura (appunti → snippet con una pressione).
- **Gestore**: 7 colori di categoria, trascinamento per riordinare / spostare, **spostamento / eliminazione in blocco multiselezione** (selezione con Ctrl / Shift, poi clic destro), annullamento eliminazione, **cestino (ripristino a 30 giorni, con anteprima del corpo)**, avviso di abbreviazioni duplicate, statistiche d'uso, modalità senza a capo per il codice, riscontro al salvataggio.
- **Dati**: JSON locale, ricaricamento a caldo (unisce automaticamente le modifiche esterne / la sincronizzazione), avviso di conflitto di sincronizzazione, esporta / importa backup, **backup automatico giornaliero (10 conservati)**, avvio con Windows.
- **Localizzazione**: **18 lingue dell'interfaccia** (cinese semplificato / tradizionale, inglese, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) con **rispecchiamento da destra a sinistra per l'arabo**, cambiate dal vivo nelle Impostazioni.
- **Robustezza**: istanza singola (un secondo avvio richiama il pannello di ricerca invece di installare due volte gli hook); la CI esegue i test più un controllo rapido della finestra a ogni push e pubblica un exe a file singolo sui tag `v*`.

## Dati e sincronizzazione

Cartella dati (predefinita `Documents\QuickText`, modificabile nelle Impostazioni, può puntare a un'unità di sincronizzazione):

```
<cartella dati>/
  ├─ index.json        # ordine delle categorie + nome file e colore di ciascuna categoria
  ├─ <category>.json   # gli snippet in quella categoria (Snippet[])
  ├─ trash.json        # snippet eliminati temporaneamente (rimossi automaticamente dopo 30 giorni)
  └─ images/           # file immagine per gli snippet con immagine
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Scritture atomiche** (`*.tmp` → `File.Replace`) così una sincronizzazione non legge mai un file scritto a metà; ricaricamento a caldo con `FileSystemWatcher` (raggruppato), con protezione dalle proprie scritture.
- Lo stato locale del computer resta **fuori dalla cartella di sincronizzazione**: le impostazioni in `%APPDATA%\QuickText\settings.json`, i conteggi d'uso / preferiti in `%APPDATA%\QuickText\usage.stats` (cambiano a ogni invio ed entrerebbero in conflitto tra computer), i backup automatici giornalieri in `%APPDATA%\QuickText\backups\`.
- **Modalità portatile** (senza tracce / USB): attivala in **Impostazioni → Dati → Modalità portatile** — inserisce un marcatore `QuickText.portable` accanto a `QuickText.exe` e **si applica al riavvio successivo** (il primo avvio portatile porta con sé le tue impostazioni e i dati d'uso, così non devi riconfigurare). Impostazioni, dati d'uso, backup e la libreria predefinita vivono quindi sotto `<cartella exe>\Data\` invece di `%APPDATA%` / Documenti, e "avvio con Windows" usa un collegamento nella cartella Esecuzione automatica anziché il registro — così l'intero strumento viaggia su una chiavetta e non lascia nulla sull'host. L'app deve trovarsi in una posizione scrivibile (una chiavetta USB, non `Program Files`); la **libreria di testo si sposta tramite Esporta / Importa backup**. L'avvio con Windows è tracciato per modalità, quindi rimetti la spunta nella nuova modalità dopo il passaggio se lo desideri. Lascialo disattivato per il layout installato descritto sopra (la scelta giusta quando la cartella dati è un'unità di sincronizzazione).<br>*(Deve trattarsi di un file marcatore, non di una semplice impostazione — decide dove risiede lo stesso `settings.json`; l'interruttore ha effetto solo al prossimo avvio, senza mai disturbare la sessione corrente.)*

## Marchio

Le risorse si trovano in `assets/branding/`: `quicktext-mark.svg` (primario), `quicktext-mark-mono.svg` (monocromatico), `quicktext.ico` (16–256, icona app / barra delle applicazioni), `quicktext-256.png`, `quicktext-social.png` (og:image 1200×630), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (scheda del marchio). Il marchio è un **cursore I-beam di testo** su una tessera color menta accanto a un blocco ambra di testo appena inserito — "text▮", ovvero mettere il tuo testo al cursore. Palette "terminal dusk": menta (l'accento dell'app `#3DC2A0`) + ambra `#F2B457` su inchiostro tendente al teal; un wordmark sans abbinato al monospazio Cascadia Code.

## Architettura

Core puro (nessun Win32, testabile a unità) tenuto separato da Win32/UI.

| Progetto | Contenuti |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 lingue) |
| `src/QuickText.App` | UI WPF (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (tema scuro), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Test unitari del Core (xUnit) |

## Compilazione ed esecuzione

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # o esegui QuickText.exe sotto bin
```

Pubblica una build portatile a file singolo (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Richiede l'SDK .NET 10. Solo Windows (scorciatoia globale Win32 / hook della tastiera / appunti).

## Informazioni sul Piano 365 open source

Questo è il progetto n. 023 del [Piano 365 open source](https://github.com/rockbenben/365opensource).

Una persona + IA, oltre 300 progetti open source in un anno. [Invia la tua idea →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Licenza

[MIT License](../LICENSE) · Copyright © 2026 rockbenben. Libero di usare, modificare e distribuire.
