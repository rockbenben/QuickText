<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · **Bahasa Indonesia** · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Bagian dari **[Rencana 365 Sumber Terbuka](https://github.com/rockbenben/365opensource)** — proyek ke-023 · pengelola snippet dan pemekar teks yang menetap di baki Windows.

**Berhenti mengetik hal yang sama dua kali.** QuickText tinggal di tray Windows: simpan teks yang Anda pakai berulang kali — email, alamat, tanda tangan, templat, balasan siap pakai, gambar — cukup sekali, lalu di **kotak input mana pun** ketik beberapa tombol atau sebuah singkatan dan teks itu langsung mendarat tepat di kursor Anda. Multi-baris, karakter khusus, dan emoji dipertahankan karakter demi karakter.

> Teks pakai-ulang Anda, hanya beberapa ketukan tombol — langsung dijatuhkan di kursor.

- WPF / .NET 10, exe portabel file tunggal, **tanpa akun, luring secara bawaan** — hanya pemeriksaan pembaruan opsional yang menghubungi GitHub.
- Data berupa **JSON lokal** di folder Anda sendiri — taruh di Dropbox / OneDrive / NAS untuk sinkronisasi.
- Tema gelap, **18 bahasa UI** (dengan pencerminan kanan-ke-kiri untuk bahasa Arab), pengaturan berlaku seketika.

---

## Di mana Anda akan memakainya

Di mana pun Anda **mengetik hal yang sama berulang-ulang di Windows**. Aplikasi ini tinggal di tray dan bekerja di setiap kotak input (jendela chat, formulir browser, editor, klien email — tidak terikat pada satu aplikasi). Ini adalah **pengelola snippet dan text expander dalam satu**, plus pencarian pinyin, templat variabel, dan gambar.

| Siapa                           | Apa yang mereka simpan                                                                                    |
| ------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Support / e-commerce**        | Balasan siap pakai, jawaban standar, teks promo, kode QR atau foto produk                                 |
| **Sales / bisnis**              | Templat email, kalimat pembuka, penawaran harga, salam penutup                                            |
| **Developer / ops**             | Perintah, konfigurasi, JSON, boilerplate (`{...}` dikeluarkan apa adanya, tak pernah diurai)              |
| **Kantor / pengisian formulir** | Email, alamat, telepon, nomor identitas, templat notulen rapat (memberi prompt, mengingat nilai terakhir) |
| **HR / admin / hukum**          | Pemberitahuan onboarding, notifikasi standar, disklaimer — lokal, offline, cocok untuk konten sensitif    |

## Lihat dalam 30 detik

Di **tempat mana pun Anda bisa mengetik** — misalnya Anda butuh email Anda di sebuah kotak chat:

1. **Panggil** — tekan `Ctrl+Shift+8`; panel muncul di atas jendela aktif.
2. **Cari** — ketik beberapa huruf; kecocokan disorot. Ia mencocokkan nama, pinyin (lengkap + inisial), singkatan, dan isi.
3. **Enter** — email ditempel **tepat di tempat kursor Anda tadi**. Selesai.

> Tanpa mengetik apa pun untuk menelusuri per kategori, dengan `Recent` / `Favorites` disematkan di atas; yang paling sering Anda pakai **naik ke atas** secara otomatis.
> `↑↓` untuk berpindah · `←→` untuk ganti kategori · `Alt+1–9` pilih cepat · klik ganda untuk mengirim · `Esc` untuk menutup.

## Tiga cara mengaksesnya — pilih yang cocok

Pustaka yang sama, tiga cara mengambilnya; campur sesuka Anda:

| Cara                    | Cara memicu                                                      | Paling cocok untuk                                           |
| ----------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------ |
| 🔍 **Pencarian panel**  | `Ctrl+Shift+8` → ketik → Enter                                   | Banyak snippet, pemakaian sesekali, menelusuri untuk memilih |
| ⌨️ **Singkatan inline** | Cukup ketik `;sig` lalu Space / Tab / Enter                      | Frasa tetap yang sering dipakai, tanpa perlu panel           |
| 🧩 **Templat variabel** | Ambil snippet dengan `{variables}` lewat salah satu cara di atas | Email / formulir: satu templat, beberapa kata diganti        |

- **Gambar juga**: tambahkan dari clipboard atau file, ditempel sebagai gambar saat dikirim; gambar bisa punya singkatan, jadi ketik singkatannya dan dapatkan gambarnya (kode QR, tanda tangan logo).
- **Output per snippet**: frasa chat terkirim otomatis setelah ditempel, snippet kode tak pernah — jadi tidak saling mengganggu.

Detail lengkap tentang singkatan dan variabel ada di **Rincian** di bawah — bagian **Placeholder** dan **Singkatan**.

---

# Rincian

## Langsung jalan tanpa setup

Klik ganda **`QuickText.exe`**; ia tinggal di **system tray** (tanpa tombol taskbar). Pada **pertama kali dijalankan** ia:

- menaruh **pustaka awal** kecil (dua kategori, dalam **bahasa UI** Anda) supaya Anda bisa langsung mencobanya;
- menampilkan balon berisi hotkey pemanggil — default **`Ctrl+Shift+8`**.

> Ikon di pojok kanan atas panel: **＋ Baru** · **Manager** (buka editor) · **Pengaturan** · **📌 Sematkan** (biarkan panel tetap terbuka setelah pengiriman, agar bisa memuntahkan beberapa berturut-turut). Langsung menuju Manager / Pengaturan tanpa harus kembali ke tray.

## Tambah / edit teks Anda

- **Tray → Buka Manager** — editor lengkap: kategori di kiri, snippet di kanan, editor di bawah. Tambah/ganti nama/hapus kategori (dengan tag **7 warna**), edit snippet (nama, singkatan, isi, gambar). Seret untuk menyusun ulang atau memindahkan antar kategori; `Ctrl+Z` membatalkan penghapusan.
- **Tray → Baru dari clipboard** — buat snippet baru dari isi clipboard saat ini dan buka di Manager untuk menyelesaikannya (disimpan saat Manager disimpan/ditutup).
- **Buat di panel** — ketik teks di kotak pencarian lalu tekan `Ctrl+N` untuk menyimpannya sebagai snippet baru (teks itu menjadi isi; `@kategori …` menaruhnya di kategori tersebut) dan langsung ke Manager untuk menyelesaikan (`Ctrl+E` mengedit yang terpilih).

## Placeholder (satu templat, banyak situasi) · dipilih per snippet

Placeholder adalah **saklar per snippet**: centang "**Aktifkan placeholder {variable}**" di editor Manager dan token di bawah akan diselesaikan saat dikirim; **jika tidak dicentang (default), isi dikirim apa adanya** — kode, skrip, dan JSON yang penuh dengan `{...}` literal tak pernah salah diurai atau ditanyakan.

> Saat memperbarui: dahulu placeholder selalu aktif. Peluncuran pertama setelah pembaruan otomatis mencentang saklar untuk **snippet yang sudah ada** yang isinya mengandung `{...}`, sehingga tidak ada perilaku yang berubah; hapus centang di Manager untuk yang sebenarnya berupa kode.

Saat diaktifkan, placeholder berikut diselesaikan saat dikirim:

| Placeholder                    | Fungsinya                                                                                                                                                                                                                                                                            |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `{name}` (label apa pun)       | **Meminta Anda mengisinya** sebelum menempel; **mengingat nilai terakhir Anda** agar pengulangan tidak menanyakan lagi                                                                                                                                                               |
| `{name:John}`                  | Variabel dengan **nilai default**, terisi otomatis di prompt                                                                                                                                                                                                                         |
| `{env\|dev\|test\|prod}`       | Variabel dengan **pilihan** — prompt menampilkan dropdown (opsi pertama sekaligus menjadi default; ketik bebas tetap diizinkan)                                                                                                                                                      |
| `{clipboard}`                  | Menyisipkan isi clipboard saat ini                                                                                                                                                                                                                                                   |
| `{cursor}`                     | Meninggalkan kursor di titik ini setelah menempel (juga menekan auto-Enter)                                                                                                                                                                                                          |
| `{date}` `{time}` `{datetime}` | Menyisipkan tanggal/waktu saat ini; mendukung offset seperti `{date+7}` (7 hari ke depan) serta format kustom seperti `{date=yyyy-MM-dd}` / `{time=HH:mm:ss}`, dapat digabung dengan offset: `{date+7=MM-dd}`. Alias bahasa Tionghoa `{日期}` / `{时间}` / `{日期时间}` juga berlaku |
| `{uuid}` `{random}`            | Nilai acak: sebuah UUID / 6 digit, baru setiap kemunculan                                                                                                                                                                                                                            |
| `{snippet:name}`               | **Menyisipkan isi snippet lain** (sedalam 3 tingkat, aman dari siklus) — simpan tanda tangan bersama di satu tempat                                                                                                                                                                  |

Contoh: sebuah tanda tangan `Best regards,\n{name}` (dengan placeholder diaktifkan) → menanyakan nama saat dikirim → menempel tanda tangan yang sudah jadi.

## Singkatan (mengembang saat Anda mengetik, tanpa panel) · opsional

Beri sebuah snippet sebuah **singkatan** untuk mengembangkannya langsung di kotak input mana pun. **Prefiks pemicu** diatur sekali di Pengaturan (default `;`):

1. Di Manager, ketik cukup **singkatannya saja** — misalnya `sig` untuk tanda tangan Anda. Prefiks ditambahkan otomatis (jangan diketik ulang); kolomnya menampilkan prefiks `;` saat ini di depannya.
2. Di kotak input mana pun ketik **`;sig`** (= prefiks + singkatan) lalu **Space / Tab / Enter** → ia menghapus `;sig` dan mengembangkan isinya (sebuah `{placeholder}` akan menanyakan lebih dulu bila snippet itu mengaktifkan placeholder).
3. **Salah ketik?** Tekan **Backspace tepat setelah** pengembangan untuk mengembalikannya menjadi `;sig` (jendela yang sama, dalam 5 detik).

Detail: pencocokan **tidak peka huruf besar/kecil** (`;SIG` tetap terpicu dengan CapsLock aktif); salah ketik yang dikoreksi dengan Backspace tetap mengembang; dua snippet yang berbagi singkatan yang sama mendapat **peringatan inline** di Manager; dan menu tray punya tombol **“Jeda pengembangan”** sekali klik (untuk demo/game).

> **Tentang prefiks:** prefiks (default `;`) mencegah pengetikan biasa / pinyin memicu palsu sebuah singkatan telanjang. Ubah di **Pengaturan → Singkatan → Prefiks pemicu** (menjadi `,`, `:`, …) atau **biarkan kosong**. Singkatan berprefiks bersifat membatasi diri sendiri dan terpicu bahkan saat menempel pada teks sebelumnya (mis. `thx;sig`); tanpa prefiks ia hanya terpicu sebagai kata tersendiri (tak pernah sebagai ekor kata yang lebih panjang seperti `graf`). Anda juga bisa menonaktifkan singkatan per aplikasi (daftar hitam, mis. `cmd.exe; putty.exe`) atau mematikannya sepenuhnya.

## Output & pengaturan umum (Tray → Pengaturan)

- **Output** — default “tempel ke aplikasi aktif”; atau “salin ke clipboard saja” (Anda menempel dengan `Ctrl+V`). Opsional: tekan Enter setelah menempel, klik tunggal untuk mengirim, pulihkan clipboard. **Setiap snippet bisa menimpa ini** (Manager → Output: ikuti global / tempel / tempel + Enter / salin saja) — frasa chat terkirim otomatis, snippet kode tak pernah.
- **Posisi panel** — mengikuti jendela aktif (default) / mengikuti kursor teks / mengingat posisi terakhir.
- **Cara memanggil (pilih satu)** — ① **kombinasi tombol**: klik kotaknya dan tekan yang baru (tombol biasa perlu `Ctrl`/`Alt`/`Shift`/`Win`; tombol fungsi **`F1`–`F24` bekerja sendirian**); atau ② **ketuk sebuah modifier**: **ketuk sekali atau dua kali satu modifier** (mis. `Ctrl` kanan, `Shift` kanan) untuk memanggil (modifier tunggal tak bisa jadi hotkey biasa, jadi dideteksi lewat ketukan). Memilih ketukan menonaktifkan kombinasi — keduanya saling eksklusif, jadi selalu jelas mana yang aktif.
- **Hotkey tangkap** — kombinasi kedua opsional yang **diam-diam menyimpan clipboard sebagai snippet baru** (umpan balik balon, tanpa jendela).
- **Folder data** — arahkan ke drive sinkronisasi; **ekspor / impor cadangan** (zip, divalidasi dengan konfirmasi penimpaan); **cadangan otomatis harian** ke mesin ini (10 terbaru disimpan, akses folder sekali klik).
- **Bahasa** — **18 bahasa**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, diganti seketika. **Mulai bersama Windows** opsional.
- **Periksa pembaruan** — nonaktif secara bawaan; jika aktif, terhubung ke GitHub sekali saat mulai untuk memeriksa versi baru (satu-satunya saat daring). «Periksa sekarang» menjalankannya sesuai permintaan.

## Contekan keyboard

| Aksi                                 | Tombol                                                           |
| ------------------------------------ | ---------------------------------------------------------------- |
| Panggil / tutup panel                | `Ctrl+Shift+8` (bisa dikonfigurasi) / `Esc`                      |
| Pilih / ganti kategori / pilih cepat | `↑↓` / `←→` / `Alt+1–9`                                          |
| Kirim                                | `Enter` atau klik ganda (klik tunggal opsional)                  |
| Favorit / batal favorit              | `Ctrl+D`                                                         |
| Buat / edit di panel                 | `Ctrl+N` / `Ctrl+E`                                              |
| Batalkan penghapusan (Manager)       | `Ctrl+Z`                                                         |
| Singkatan: picu / batalkan           | ketik singkatan + Space·Tab·Enter / Backspace setelah mengembang |

---

## Fitur sekilas

- **Pemanggilan**: hotkey global — sebuah **kombinasi tombol** (tombol fungsi bekerja sebagai satu tombol) atau **ketuk sekali/dua kali sebuah modifier** (mis. Ctrl kanan), **pilih satu**; panel mengikuti jendela aktif / kursor teks / posisi yang diingat; tombol baris atas melompat ke **Baru / Manager / Pengaturan**; sematkan untuk mengirim beberapa berturut-turut; seret & ubah ukuran dengan ukuran yang diingat.
- **Pencarian**: nama / pinyin / inisial / singkatan / isi, disorot; **seri diputus berdasarkan frekuensi pemakaian (frecency)**; `@category keywords` mempersempit pencarian ke satu kategori (`@category` telanjang menelusurinya).
- **Konten**: teks polos (multi-baris, karakter khusus, emoji tanpa kehilangan), placeholder (default / dropdown opsi / snippet bersarang / format tanggal kustom / uuid / random — **dipilih per snippet**), **gambar** (dari clipboard atau file, ditempel sebagai gambar saat dikirim; **gambar bisa punya singkatan juga** — ketik singkatannya, dapatkan gambarnya).
- **Singkatan**: dipicu terminator, prompt variabel, batalkan sekali tekan, koreksi salah ketik dengan Backspace, tidak peka huruf besar/kecil, klik memecah token, peringatan duplikat, daftar hitam per aplikasi, **jeda tray sekali klik**.
- **Output**: tempel langsung / salin saja; auto-Enter opsional, pulihkan clipboard, kirim klik tunggal; **penimpaan output per snippet**; hotkey tangkap (clipboard → snippet dalam satu tekan).
- **Manager**: 7 warna kategori, seret susun ulang / pindah, **pilih banyak untuk pindah / hapus massal** (pilih dengan Ctrl / Shift, lalu klik kanan), batalkan penghapusan, **tempat sampah (pulihkan 30 hari, dengan pratinjau isi)**, peringatan singkatan duplikat, statistik pemakaian, mode tanpa bungkus untuk kode, umpan balik penyimpanan.
- **Data**: JSON lokal, hot-reload (menggabungkan otomatis suntingan eksternal / sinkronisasi), pemberitahuan konflik sinkronisasi, ekspor / impor cadangan, **cadangan otomatis harian (10 disimpan)**, mulai bersama Windows.
- **Lokalisasi**: **18 bahasa UI** (Tionghoa Sederhana / Tradisional, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) dengan **pencerminan kanan-ke-kiri untuk bahasa Arab**, diganti langsung di Pengaturan.
- **Ketahanan**: instance tunggal (peluncuran kedua memanggil panel pencarian alih-alih memasang hook dua kali); CI menjalankan tes plus pemeriksaan asap jendela pada setiap push dan menerbitkan exe file tunggal pada tag `v*`.

## Data & sinkronisasi

Folder data (default `Documents\QuickText`, bisa diubah di Pengaturan, dapat mengarah ke drive sinkronisasi):

```
<data folder>/
  ├─ index.json        # urutan kategori + nama file dan warna tiap kategori
  ├─ <category>.json   # snippet dalam kategori itu (Snippet[])
  ├─ trash.json        # snippet yang dihapus lunak (dibersihkan otomatis setelah 30 hari)
  └─ images/           # file gambar untuk snippet gambar
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Penulisan atomik** (`*.tmp` → `File.Replace`) agar sinkronisasi tak pernah membaca file yang setengah tertulis; hot-reload `FileSystemWatcher` (digabungkan), dengan penjaga tulisan-sendiri.
- Status lokal-mesin tetap **di luar folder sinkronisasi**: pengaturan di `%APPDATA%\QuickText\settings.json`, hitungan pemakaian / favorit di `%APPDATA%\QuickText\usage.stats` (keduanya berubah setiap pengiriman dan akan bentrok antar mesin), cadangan otomatis harian di `%APPDATA%\QuickText\backups\`.
- **Mode portabel** (tanpa jejak / USB): aktifkan di **Pengaturan → Data → Mode portabel** — ia menaruh penanda `QuickText.portable` di sebelah `QuickText.exe` dan **berlaku pada restart berikutnya** (start portabel pertama membawa pengaturan dan pemakaian Anda ikut, jadi Anda tak perlu mengatur ulang). Pengaturan, pemakaian, cadangan, dan pustaka default lalu tinggal di bawah `<exe folder>\Data\` alih-alih `%APPDATA%` / Documents, dan "mulai bersama Windows" memakai pintasan folder Startup ketimbang registri — sehingga seluruh alat bisa dibawa di flash disk dan tak meninggalkan apa pun di host. Aplikasi harus berada di lokasi yang bisa ditulis (flash disk USB, bukan `Program Files`); **pustaka teks berpindah lewat Ekspor / Impor cadangan**. Mulai-bersama-Windows dilacak per mode, jadi centang lagi di mode baru setelah beralih bila Anda menginginkannya. Biarkan mati untuk tata letak terpasang di atas (pilihan yang tepat saat folder data adalah drive sinkronisasi).<br>_(Ini harus berupa file penanda, bukan pengaturan biasa — ia menentukan di mana `settings.json` itu sendiri tinggal; saklarnya baru berlaku pada start berikutnya, tak pernah mengganggu sesi saat ini.)_

## Merek

Aset ada di `assets/branding/`: `quicktext-mark.svg` (utama), `quicktext-mark-mono.svg` (monokrom), `quicktext.ico` (16–256, ikon aplikasi / tray), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (lembar merek). Markanya adalah **kursor I-beam teks** di atas ubin mint di samping blok amber berisi teks yang baru mendarat — “text▮”, yakni menempatkan teks Anda di kursor. Palet “terminal dusk”: mint (aksen aplikasi `#3DC2A0`) + amber `#F2B457` di atas tinta yang condong ke teal; wordmark sans dipadukan dengan mono Cascadia Code.

## Arsitektur

Core Murni (tanpa Win32, bisa diuji unit) dipisahkan dari Win32/UI.

| Proyek                       | Isi                                                                                                                                                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/QuickText.Core`         | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 bahasa)                |
| `src/QuickText.App`          | UI WPF (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (tema gelap), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Tes unit Core (xUnit)                                                                                                                                                                                                |

## Build & jalankan

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # atau jalankan QuickText.exe di bawah bin
```

Terbitkan build portabel file tunggal (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Membutuhkan .NET 10 SDK. Hanya Windows (hotkey global Win32 / keyboard hook / clipboard).

## Tentang Rencana 365 Sumber Terbuka

Ini adalah proyek ke-023 dari [Rencana 365 Sumber Terbuka](https://github.com/rockbenben/365opensource).

Satu orang + AI, 300+ proyek sumber terbuka dalam setahun. [Kirim ide Anda →](https://365.aishort.top/)

## Lisensi

[MIT License](../LICENSE) · Hak Cipta © 2026 rockbenben. Bebas digunakan, dimodifikasi, dan didistribusikan.
