<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [Tiếng Việt](README.vi.md) · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · **Türkçe**

# QuickText

> **[365 Açık Kaynak Planı](https://github.com/rockbenben/365opensource)** kapsamında 023. proje · Windows sistem tepsisinde çalışan snippet yöneticisi ve metin genişletici.

**Aynı şeyi iki kez yazmayı bırakın.** QuickText, Windows sistem tepsisinde durur: tekrar tekrar başvurduğunuz metinleri — e-posta, adresler, imzalar, şablonlar, hazır yanıtlar, görseller — bir kez saklayın, ardından **herhangi bir giriş kutusunda** birkaç tuşa veya bir kısaltmaya basın; metin tam imlecinizin olduğu yere düşsün. Çok satırlı içerik, özel karakterler ve emojiler karakter karakter korunur.

> Yeniden kullanabileceğiniz metinleriniz birkaç tuş ötede — doğrudan imlece bırakılır.

- WPF / .NET 10, tek dosyalık taşınabilir exe, **hesap yok, varsayılan olarak çevrimdışı** — yalnızca isteğe bağlı güncelleme denetimi GitHub'a bağlanır.
- Veriler kendi klasörünüzdeki **yerel JSON** olarak durur — senkronize etmek için Dropbox / OneDrive / bir NAS içine koyun.
- Koyu tema, **18 arayüz dili** (Arapça için sağdan sola yansıtmayla), ayarlar anında uygulanır.

---

## Nerelerde kullanırsınız

**Windows'ta aynı şeyi tekrar tekrar yazdığınız** her yerde. Tepside durur ve her giriş kutusunda çalışır (sohbet pencereleri, tarayıcı formları, editörler, e-posta istemcileri — tek bir uygulamaya bağlı değildir). Tek başına bir **snippet yöneticisi ve metin genişletici** olmasının yanında pinyin arama, değişken şablonları ve görseller de sunar.

| Kim | Ne saklar |
|---|---|
| **Destek / e-ticaret** | Hazır yanıtlar, standart cevaplar, promosyon metinleri, QR kodları veya ürün fotoğrafları |
| **Satış / iş geliştirme** | E-posta şablonları, açılış cümleleri, teklifler, kapanış imzaları |
| **Geliştiriciler / operasyon** | Komutlar, yapılandırma, JSON, taslak kod (`{...}` olduğu gibi çıkar, asla ayrıştırılmaz) |
| **Ofis / form doldurma** | E-posta, adres, telefon, kimlik numaraları, toplantı notu şablonları (size sorar, son değeri hatırlar) |
| **İK / idari / hukuk** | İşe alım bildirimleri, standart bildirimler, sorumluluk reddi metinleri — yerel, çevrimdışı, hassas içeriğe uygun |

## 30 saniyede görün

**Yazı yazabildiğiniz herhangi bir yerde** — diyelim ki bir sohbet kutusunda e-postanız lazım:

1. **Çağırın** — `Ctrl+Shift+8` tuşlarına basın; panel etkin pencerenin üzerinde belirir.
2. **Arayın** — birkaç harf yazın; eşleşmeler vurgulanır. Ad, pinyin (tam + baş harfler), kısaltma ve gövde üzerinden eşleşir.
3. **Enter** — e-posta **imlecinizin bulunduğu yere** yapıştırılır. Bitti.

> Kategoriye göre göz atmak için hiçbir şey yazmayın; `Son kullanılanlar` / `Sık kullanılanlar` en üste sabitlenir; en çok kullandıklarınız otomatik olarak **üste çıkar**.
> `↑↓` gezinir · `←→` kategori değiştirir · `Alt+1–9` hızlı seçim · göndermek için çift tıklama · kapatmak için `Esc`.

## Ona ulaşmanın üç yolu — size uyanı seçin

Aynı kitaplık, ondan alma üç yolu; serbestçe karıştırın:

| Yol | Nasıl tetiklenir | Ne için en iyi |
|---|---|---|
| 🔍 **Panel araması** | `Ctrl+Shift+8` → yaz → Enter | Çok sayıda snippet, ara sıra kullanım, seçerek göz atma |
| ⌨️ **Satır içi kısaltma** | Sadece `;sig` yazın, ardından Space / Tab / Enter | Yüksek frekanslı sabit ifadeler, panele gerek yok |
| 🧩 **Değişken şablonu** | Yukarıdakilerden biriyle `{variables}` içeren bir snippet çekin | E-posta / formlar: tek şablon, birkaç kelime değişir |

- **Görseller de**: panodan veya dosyadan ekleyin, gönderirken görsel olarak yapıştırılır; görsellerin de kısaltması olabilir, yani kısaltmayı yazın ve resmi alın (QR kodları, logolu imzalar).
- **Snippet başına çıktı**: sohbet ifadeleri yapıştırmadan sonra otomatik gönderilir, kod snippet'leri asla gönderilmez — birbirine karışmazlar.

Kısaltmalar ve değişkenlerle ilgili tüm ayrıntılar aşağıdaki **Ayrıntılı bilgi** bölümünde — **Yer tutucular** ve **Kısaltmalar** başlıkları altında.

---

# Ayrıntılı bilgi

## Kutudan çıktığı gibi çalışır

**`QuickText.exe`** dosyasına çift tıklayın; **sistem tepsisinde** durur (görev çubuğu düğmesi yok). **İlk çalıştırmada** şunları yapar:

- hemen deneyebilmeniz için küçük bir **başlangıç kitaplığı** bırakır (**arayüz dilinizde** iki kategori);
- çağırma kısayolunu gösteren bir balon açar — varsayılan **`Ctrl+Shift+8`**.

> Panelin sağ üstündeki simgeler: **＋ Yeni** · **Yönetici** (editörü açar) · **Ayarlar** · **📌 Sabitle** (bir gönderimin ardından paneli açık tutarak arka arkaya birkaç tanesini gönderin). Tepsiye dönmeden doğrudan Yönetici / Ayarlar bölümüne geçin.

## Metninizi ekleyin / düzenleyin

- **Tepsi → Yöneticiyi Aç** — tam editör: solda kategoriler, sağda snippet'ler, altta editör. Kategori ekleyin/yeniden adlandırın/silin (**7 renkli** bir etiketle), snippet'leri düzenleyin (ad, kısaltma, gövde, görsel). Yeniden sıralamak veya kategoriler arasında taşımak için sürükleyin; `Ctrl+Z` bir silmeyi geri alır.
- **Tepsi → Panodan yeni** — mevcut panodan yeni bir snippet oluşturun ve tamamlamak için Yönetici'de açın (Yönetici kaydedildiğinde/kapatıldığında kaydedilir).
- **Panelde oluşturun** — arama kutusuna metni yazın ve `Ctrl+N` tuşuna basarak yeni bir parça olarak kaydedin (bu metin gövde olur; `@kategori …` onu o kategoriye yerleştirir) ve tamamlamak üzere Yönetici'ye geçin (`Ctrl+E` seçili olanı düzenler).

## Yer tutucular (tek şablon, birçok durum) · snippet başına isteğe bağlı

Yer tutucular **snippet başına bir anahtardır**: Yönetici editöründe "**{variable} yer tutucularını etkinleştir**" seçeneğini işaretleyin; aşağıdaki tokenlar gönderimde çözümlenir; **işaretlenmediğinde (varsayılan), gövde olduğu gibi gönderilir** — düz `{...}` dolu kod, script ve JSON asla yanlış ayrıştırılmaz veya sorulmaz.

> Yükseltme: yer tutucular eskiden her zaman açıktı. Yükseltmeden sonraki ilk başlatma, gövdesinde `{...}` bulunan **mevcut snippet'ler** için anahtarı otomatik olarak işaretler, böylece davranış değişmez; gerçekten kod olanlar için Yönetici'de işareti kaldırın.

Etkinleştirildiğinde, bu yer tutucular gönderimde çözümlenir:

| Yer tutucu | Ne yapar |
|---|---|
| `{name}` (herhangi bir etiket) | Yapıştırmadan önce **doldurmanızı ister**; **son değerinizi hatırlar**, böylece tekrarlar yeniden sormaz |
| `{name:John}` | **Varsayılan** değerli değişken, istemde önceden doldurulmuş |
| `{env\|dev\|test\|prod}` | **Seçenekli** değişken — istem bir açılır liste gösterir (ilk seçenek aynı zamanda varsayılandır; serbest yazma yine de mümkündür) |
| `{clipboard}` | Mevcut pano içeriğini ekler |
| `{cursor}` | Yapıştırmadan sonra imleci bu noktada bırakır (otomatik Enter'ı da bastırır) |
| `{date}` `{time}` `{datetime}` | Geçerli tarihi/saati ekler; `{date+7}` (7 gün sonrası) gibi kaymaları destekler. Çince takma adlar `{日期}` / `{时间}` / `{日期时间}` de çalışır |
| `{uuid}` `{random}` | Rastgele değerler: bir UUID / 6 basamak, her oluşumda taze |
| `{snippet:name}` | **Başka bir snippet'in gövdesini satır içine alır** (3 seviye derinlik, döngü güvenli) — ortak imzaları tek bir yerde tutun |

Örnek: `Best regards,\n{name}` imzası (yer tutucular etkinken) → gönderimde adı sorar → tamamlanmış imzayı yapıştırır.

## Kısaltmalar (yazarken genişletin, panel yok) · isteğe bağlı

Bir snippet'e herhangi bir giriş kutusunda doğrudan genişletmek için bir **kısaltma** verin. **Tetikleme öneki** bir kez Ayarlar'da belirlenir (varsayılan `;`):

1. Yönetici'de sadece **kısaltmanın kendisini** yazın — örneğin imzanız için `sig`. Önek otomatik olarak eklenir (yeniden yazmayın); alan, önünde geçerli `;` önekini gösterir.
2. Herhangi bir giriş kutusuna **`;sig`** (= önek + kısaltma) yazın, ardından **Space / Tab / Enter** → `;sig`'i siler ve gövdeyi genişletir (o snippet'te yer tutucular etkinse önce bir `{placeholder}` sorar).
3. **Yanlış mı yazdınız?** Genişletmeden **hemen sonra** Backspace'e basarak onu tekrar `;sig`'e döndürün (aynı pencere, 5 sn içinde).

Ayrıntılar: eşleştirme **büyük/küçük harfe duyarlı değildir** (`;SIG`, CapsLock açıkken tetiklenir); Backspace ile düzeltilen bir yazım hatası yine de genişler; aynı kısaltmayı paylaşan iki snippet Yönetici'de **satır içi uyarı** alır; ve tepsi menüsünde tek tıkla **"Genişletmeyi duraklat"** anahtarı vardır (demolar/oyunlar için).

> **Önek hakkında:** önek (varsayılan `;`), sıradan yazmanın / pinyin'in çıplak bir kısaltmayı yanlışlıkla tetiklemesini engeller. **Ayarlar → Kısaltmalar → Tetikleme öneki** altında değiştirin (`,`, `:`, … olarak) veya **boş bırakın**. Önekli bir kısaltma kendi kendini sınırlar ve önündeki metne yapışık olsa bile tetiklenir (örneğin `thx;sig`); önek olmadan yalnızca kendi kelimesi olarak tetiklenir (asla `graf` gibi daha uzun bir kelimenin kuyruğu olarak değil). Kısaltmaları uygulama başına da devre dışı bırakabilir (bir kara liste, örneğin `cmd.exe; putty.exe`) veya tamamen kapatabilirsiniz.

## Çıktı ve genel ayarlar (Tepsi → Ayarlar)

- **Çıktı** — varsayılan "etkin uygulamaya yapıştır"; veya "yalnızca panoya kopyala" (`Ctrl+V` ile siz yapıştırırsınız). İsteğe bağlı: yapıştırmadan sonra Enter'a bas, göndermek için tek tıklama, panoyu geri yükle. **Her snippet bunu geçersiz kılabilir** (Yönetici → Çıktı: geneli takip et / yapıştır / yapıştır + Enter / yalnızca kopyala) — sohbet ifadeleri otomatik gönderilir, kod snippet'leri asla gönderilmez.
- **Panel konumu** — etkin pencereyi takip et (varsayılan) / metin imlecini takip et / son konumu hatırla.
- **Çağırma yöntemi (birini seçin)** — ① **tuş kombinasyonu**: kutuya tıklayın ve yeni bir tane basın (sıradan tuşlar `Ctrl`/`Alt`/`Shift`/`Win` gerektirir; işlev tuşları **`F1`–`F24` tek başına çalışır**); veya ② **bir değiştirici tuşa dokunun**: çağırmak için **bir değiştirici tuşa tek veya çift dokunun** (örneğin sağ `Ctrl`, sağ `Shift`) (tek başına bir değiştirici tuş normal kısayol olamayacağından dokunuşla algılanır). Dokunmayı seçmek kombinasyonu devre dışı bırakır — ikisi birbirini dışlar, böylece hangisinin etkin olduğu her zaman nettir.
- **Yakalama kısayolu** — panoyu **sessizce yeni bir snippet olarak kaydeden** isteğe bağlı ikinci bir kombinasyon (balon geri bildirimi, pencere yok).
- **Veri klasörü** — bir senkronizasyon sürücüsüne yönlendirin; **yedeği dışa / içe aktar** (zip, üzerine yazma onayıyla doğrulanır); bu makineye günlük **otomatik yedekleme** (en yeni 10 tanesi tutulur, tek tıkla klasör erişimi).
- **Dil** — **18 dil**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, anında değişir. **Windows ile başlat** isteğe bağlı.
- **Güncellemeleri denetle** — varsayılan olarak kapalı; açıkken başlangıçta bir kez GitHub'a bağlanarak yeni sürüm olup olmadığını denetler (çevrimiçi olduğu tek an). «Şimdi denetle» ile isteğe bağlı çalıştırın.

## Klavye kısa referansı

| Eylem | Tuşlar |
|---|---|
| Paneli çağır / kapat | `Ctrl+Shift+8` (yapılandırılabilir) / `Esc` |
| Seç / kategori değiştir / hızlı seçim | `↑↓` / `←→` / `Alt+1–9` |
| Gönder | `Enter` veya çift tıklama (tek tıklama isteğe bağlı) |
| Sık kullanılana ekle / çıkar | `Ctrl+D` |
| Panelde oluştur / düzenle | `Ctrl+N` / `Ctrl+E` |
| Silmeyi geri al (Yönetici) | `Ctrl+Z` |
| Kısaltma: tetikle / geri al | kısaltma yaz + Space·Tab·Enter / genişlettikten sonra Backspace |

---

## Bir bakışta özellikler

- **Çağırma**: global kısayol — bir **tuş kombinasyonu** (işlev tuşları tek tuş olarak çalışır) veya **bir değiştirici tuşa tek/çift dokunma** (örneğin sağ Ctrl), **birini seçin**; panel etkin pencereyi / metin imlecini / hatırlanan konumu takip eder; üst sıra düğmeleri **Yeni / Yönetici / Ayarlar** bölümüne atlar; arka arkaya birkaç tane göndermek için sabitleyin; hatırlanan boyutla sürükleyip yeniden boyutlandırın.
- **Arama**: ad / pinyin / baş harfler / kısaltma / gövde, vurgulanmış; **eşitlikler kullanım sıklığıyla (frecency) çözülür**; `@category keywords` aramayı tek bir kategoriyle sınırlar (çıplak `@category` ona göz atar).
- **İçerik**: düz metin (çok satırlı, özel karakterler, kayıpsız emoji), yer tutucular (varsayılanlar / seçenek açılır listeleri / snippet iç içe geçirme / uuid / random — **snippet başına isteğe bağlı**), **görseller** (panodan veya dosyadan, gönderimde görsel olarak yapıştırılır; **görsellerin de kısaltması olabilir** — kısaltmayı yazın, görseli alın).
- **Kısaltmalar**: sonlandırıcı tetiklemeli, değişken istemi, tek tuşla geri alma, Backspace ile yazım düzeltme, büyük/küçük harfe duyarsız, tıklama tokenı böler, yinelenme uyarısı, uygulama başına kara liste, **tek tıkla tepsiden duraklatma**.
- **Çıktı**: doğrudan yapıştır / yalnızca kopyala; isteğe bağlı otomatik Enter, panoyu geri yükleme, tek tıkla gönderme; **snippet başına çıktı geçersiz kılma**; yakalama kısayolu (pano → tek basışta snippet).
- **Yönetici**: 7 kategori rengi, sürükleyerek sıralama / taşıma, **çoklu seçimle toplu taşıma / silme** (Ctrl / Shift ile seçin, sonra sağ tıklayın), silmeyi geri alma, **çöp kutusu (30 günlük geri yükleme, gövde önizlemesiyle)**, yinelenen kısaltma uyarısı, kullanım istatistikleri, kod için kaydırmasız mod, kaydetme geri bildirimi.
- **Veri**: yerel JSON, sıcak yeniden yükleme (dış düzenlemeleri / senkronizasyonu otomatik birleştirir), senkronizasyon çakışması bildirimi, yedeği dışa / içe aktarma, **günlük otomatik yedekleme (10 tanesi tutulur)**, Windows ile başlatma.
- **Yerelleştirme**: **18 arayüz dili** (Basitleştirilmiş / Geleneksel Çince, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …), **Arapça için sağdan sola yansıtmayla**, Ayarlar'da canlı olarak değişir.
- **Sağlamlık**: tek örnek (ikinci bir başlatma, kancaları iki kez kurmak yerine arama panelini çağırır); CI her push'ta testleri artı bir pencere duman kontrolü çalıştırır ve `v*` etiketlerinde tek dosyalık bir exe yayınlar.

## Veri ve senkronizasyon

Veri klasörü (varsayılan `Documents\QuickText`, Ayarlar'da değiştirilebilir, bir senkronizasyon sürücüsüne yönlendirilebilir):

```
<data folder>/
  ├─ index.json        # category order + each category's file name and color
  ├─ <category>.json   # the snippets in that category (Snippet[])
  ├─ trash.json        # soft-deleted snippets (auto-purged after 30 days)
  └─ images/           # image files for image snippets
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Atomik yazma** (`*.tmp` → `File.Replace`), böylece bir senkronizasyon asla yarı yazılmış bir dosyayı okumaz; `FileSystemWatcher` sıcak yeniden yüklemesi (birleştirilmiş), bir kendi kendine yazma korumasıyla.
- Makineye özel durum **senkronizasyon klasörünün dışında kalır**: ayarlar `%APPDATA%\QuickText\settings.json` içinde, kullanım sayıları / sık kullanılanlar `%APPDATA%\QuickText\usage.stats` içinde (her gönderimde değişirler ve makineler arasında çakışırlar), günlük otomatik yedeklemeler `%APPDATA%\QuickText\backups\` içinde.
- **Taşınabilir mod** (iz bırakmayan / USB): **Ayarlar → Veri → Taşınabilir mod** altında açın — `QuickText.exe` yanına bir `QuickText.portable` işaretçisi bırakır ve **bir sonraki yeniden başlatmada uygulanır** (ilk taşınabilir başlatma, ayarlarınızı ve kullanımınızı beraberinde taşır, böylece yeniden yapılandırmazsınız). Ayarlar, kullanım, yedeklemeler ve varsayılan kitaplık bundan sonra `%APPDATA%` / Documents yerine `<exe folder>\Data\` altında yaşar ve "Windows ile başlat" kayıt defteri yerine bir Başlangıç klasörü kısayolu kullanır — böylece tüm araç bir bellekte gezer ve ana bilgisayarda hiçbir şey bırakmaz. Uygulama yazılabilir bir konumda bulunmalıdır (bir USB bellek, `Program Files` değil); **metin kitaplığı Dışa / İçe aktar yedeği aracılığıyla taşınır**. Windows ile başlatma mod başına izlenir, bu nedenle geçiş yaptıktan sonra isterseniz yeni modda tekrar işaretleyin. Yukarıdaki kurulu düzen için kapalı bırakın (veri klasörü bir senkronizasyon sürücüsü olduğunda doğru seçim).<br>*(Bir işaretçi dosyası olması gerekir, düz bir ayar değil — `settings.json` dosyasının kendisinin nerede yaşayacağına karar verir; anahtar yalnızca bir sonraki başlatmada etkinleşir, mevcut oturumu asla bozmaz.)*

## Marka

Varlıklar `assets/branding/` içinde yaşar: `quicktext-mark.svg` (birincil), `quicktext-mark-mono.svg` (tek renk), `quicktext.ico` (16–256, uygulama / tepsi simgesi), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (marka sayfası). Marka, yeni düşmüş amber bir metin bloğunun yanında nane yeşili bir karonun üzerinde bir **metin I-imlecidir** — "text▮", yani metninizi imlece koymak. "terminal alacakaranlığı" paleti: nane yeşili (uygulama vurgusu `#3DC2A0`) + amber `#F2B457`, mavimsi-yeşil mürekkep üzerinde; bir sans wordmark, Cascadia Code mono ile eşleştirilmiş.

## Mimari

Saf Core (Win32 yok, birim testi yapılabilir), Win32/UI'dan ayrı tutulur.

| Proje | İçerik |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 dil) |
| `src/QuickText.App` | WPF UI (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (koyu tema), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Core birim testleri (xUnit) |

## Derleme ve çalıştırma

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # or run QuickText.exe under bin
```

Tek dosyalık taşınabilir bir derleme yayınlayın (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

.NET 10 SDK gerektirir. Yalnızca Windows (Win32 global kısayol / klavye kancası / pano).

## 365 Açık Kaynak Planı hakkında

Bu, [365 Açık Kaynak Planı](https://github.com/rockbenben/365opensource) kapsamındaki 023. projedir.

Bir kişi + yapay zekâ, bir yılda 300'den fazla açık kaynak projesi. [Fikrini gönder →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Lisans

[MIT License](../LICENSE) · Telif hakkı © 2026 rockbenben. Kullanmakta, değiştirmekte ve dağıtmakta özgürsünüz.
