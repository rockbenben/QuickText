<p align="left">
  <img src="../assets/branding/quicktext-256.png" width="72" alt="QuickText">
</p>

[English](../README.md) · [简体中文](../README.zh.md) · [繁體中文](README.zh-Hant.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Português](README.pt.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · **Tiếng Việt** · [ไทย](README.th.md) · [Bahasa Indonesia](README.id.md) · [हिन्दी](README.hi.md) · [বাংলা](README.bn.md) · [العربية](README.ar.md) · [Türkçe](README.tr.md)

# QuickText

> Thuộc **[Kế hoạch 365 mã nguồn mở](https://github.com/rockbenben/365opensource)** — dự án số 023 · trình quản lý đoạn văn bản kiêm mở rộng văn bản thường trú ở khay Windows.

**Đừng gõ đi gõ lại cùng một thứ.** QuickText nằm trong khay hệ thống của Windows: hãy lưu một lần những đoạn văn bản bạn dùng đi dùng lại — email, địa chỉ, chữ ký, mẫu văn bản, câu trả lời soạn sẵn, hình ảnh — rồi trong **bất kỳ ô nhập liệu nào**, chỉ cần gõ vài phím hoặc một từ viết tắt là nó xuất hiện ngay tại con trỏ của bạn. Nhiều dòng, ký tự đặc biệt và emoji được giữ nguyên vẹn từng ký tự.

> Văn bản tái sử dụng của bạn, chỉ cách vài phím gõ — thả thẳng vào ngay tại con trỏ.

- WPF / .NET 10, exe di động dạng đơn tệp, **không cần tài khoản, ngoại tuyến theo mặc định** — chỉ việc kiểm tra cập nhật tùy chọn mới kết nối GitHub.
- Dữ liệu là **JSON cục bộ** trong thư mục của riêng bạn — đặt nó vào Dropbox / OneDrive / một NAS để đồng bộ.
- Giao diện tối, **18 ngôn ngữ giao diện** (có phản chiếu phải-sang-trái cho tiếng Ả Rập), thiết lập áp dụng tức thì.

---

## Bạn sẽ dùng nó ở đâu

Bất cứ nơi nào bạn **gõ đi gõ lại cùng một thứ trên Windows**. Nó nằm trong khay hệ thống và hoạt động trong mọi ô nhập liệu (cửa sổ chat, biểu mẫu trình duyệt, trình soạn thảo, ứng dụng email — không bị bó buộc vào một ứng dụng nào). Đây là **trình quản lý đoạn văn bản và bộ mở rộng văn bản gộp làm một**, cộng thêm tìm kiếm bằng bính âm, mẫu biến, và hình ảnh.

| Ai | Họ lưu gì |
|---|---|
| **Hỗ trợ / thương mại điện tử** | Câu trả lời soạn sẵn, câu trả lời chuẩn, đoạn quảng cáo, mã QR hoặc ảnh sản phẩm |
| **Kinh doanh / doanh nghiệp** | Mẫu email, câu mở đầu, báo giá, câu kết thư |
| **Lập trình viên / vận hành** | Câu lệnh, cấu hình, JSON, mã khung (`{...}` phát nguyên văn, không bao giờ bị phân tích) |
| **Văn phòng / điền biểu mẫu** | Email, địa chỉ, số điện thoại, số CMND, mẫu ghi chú cuộc họp (nhắc bạn nhập, ghi nhớ giá trị lần trước) |
| **Nhân sự / hành chính / pháp lý** | Thông báo nhập việc, thông báo chuẩn, tuyên bố miễn trừ — cục bộ, ngoại tuyến, phù hợp cho nội dung nhạy cảm |

## Xem trong 30 giây

Ở **bất kỳ nơi nào bạn có thể gõ** — chẳng hạn bạn cần email của mình trong một ô chat:

1. **Gọi ra** — nhấn `Ctrl+Shift+8`; bảng điều khiển hiện lên trên cửa sổ đang hoạt động.
2. **Tìm kiếm** — gõ vài chữ cái; các kết quả khớp được tô sáng. Nó khớp với tên, bính âm (đầy đủ + chữ cái đầu), từ viết tắt, và nội dung.
3. **Enter** — email được dán **ngay tại nơi con trỏ của bạn vừa ở**. Xong.

> Không gõ gì để duyệt theo danh mục, với `Gần đây` / `Yêu thích` được ghim lên đầu; những mục bạn dùng nhiều nhất **tự động nổi lên trên cùng**.
> `↑↓` di chuyển · `←→` chuyển danh mục · `Alt+1–9` chọn nhanh · nhấp đúp để gửi · `Esc` để đóng.

## Ba cách để dùng nó — chọn cái phù hợp

Cùng một thư viện, ba cách để lấy ra; hãy kết hợp chúng thoải mái:

| Cách | Cách kích hoạt | Phù hợp nhất cho |
|---|---|---|
| 🔍 **Tìm kiếm trên bảng** | `Ctrl+Shift+8` → gõ → Enter | Nhiều đoạn văn bản, dùng thỉnh thoảng, duyệt để chọn |
| ⌨️ **Từ viết tắt nội dòng** | Chỉ cần gõ `;sig` rồi Space / Tab / Enter | Câu cố định dùng tần suất cao, không cần bảng điều khiển |
| 🧩 **Mẫu biến** | Lấy một đoạn văn bản có `{biến}` qua một trong hai cách trên | Email / biểu mẫu: một mẫu, thay đổi vài từ |

- **Cả hình ảnh nữa**: thêm từ clipboard hoặc tệp, được dán dưới dạng hình ảnh khi gửi; hình ảnh có thể có từ viết tắt, nên gõ từ viết tắt là được hình (mã QR, chữ ký logo).
- **Đầu ra theo từng đoạn**: câu chat tự động gửi sau khi dán, đoạn mã thì không bao giờ — chúng không gây cản trở.

Chi tiết đầy đủ về từ viết tắt và biến nằm trong phần **Chi tiết** bên dưới — các mục **Trình giữ chỗ** và **Từ viết tắt**.

---

# Chi tiết

## Dùng được ngay

Nhấp đúp **`QuickText.exe`**; nó nằm trong **khay hệ thống** (không có nút trên thanh tác vụ). Ở **lần chạy đầu tiên** nó:

- đưa vào một **thư viện khởi đầu** nhỏ (hai danh mục, theo **ngôn ngữ giao diện** của bạn) để bạn có thể thử ngay;
- hiển thị một bong bóng với phím tắt gọi ra — mặc định **`Ctrl+Shift+8`**.

> Các biểu tượng góc trên bên phải của bảng: **＋ Mới** · **Trình quản lý** (mở trình soạn thảo) · **Cài đặt** · **📌 Ghim** (giữ bảng mở sau khi gửi, để phát liên tiếp nhiều đoạn). Nhảy thẳng đến Trình quản lý / Cài đặt mà không phải quay lại khay hệ thống.

## Thêm / chỉnh sửa văn bản của bạn

- **Khay → Mở Trình quản lý** — trình soạn thảo đầy đủ: danh mục bên trái, đoạn văn bản bên phải, trình soạn thảo bên dưới. Thêm/đổi tên/xóa danh mục (với thẻ **7 màu**), chỉnh sửa đoạn văn bản (tên, từ viết tắt, nội dung, hình ảnh). Kéo để sắp xếp lại hoặc chuyển giữa các danh mục; `Ctrl+Z` hoàn tác một thao tác xóa.
- **Khay → Tạo mới từ clipboard** — tạo một đoạn văn bản mới từ clipboard hiện tại và mở nó trong Trình quản lý để hoàn thiện (được lưu khi Trình quản lý được lưu/đóng).
- **Tạo trong bảng** — gõ nội dung vào ô tìm kiếm và nhấn `Ctrl+N` để lưu thành đoạn mới (văn bản đó trở thành nội dung; `@danh mục …` xếp nó vào danh mục đó) và nhảy đến Trình quản lý để hoàn thiện (`Ctrl+E` chỉnh sửa mục đang chọn).

## Trình giữ chỗ (một mẫu, nhiều tình huống) · bật tùy chọn theo từng đoạn

Trình giữ chỗ là một **công tắc theo từng đoạn**: đánh dấu "**Bật trình giữ chỗ {biến}**" trong trình soạn thảo của Trình quản lý và các token bên dưới sẽ được giải quyết khi gửi; **để không đánh dấu (mặc định), nội dung được gửi nguyên văn** — mã, script và JSON đầy `{...}` nguyên văn không bao giờ bị phân tích sai hoặc bị hỏi.

> Nâng cấp: trình giữ chỗ trước đây luôn bật. Lần khởi động đầu tiên sau khi nâng cấp tự động đánh dấu công tắc cho **các đoạn văn bản hiện có** có nội dung chứa `{...}`, nên không có gì thay đổi về hành vi; bỏ đánh dấu nó trong Trình quản lý cho những đoạn thực sự là mã.

Khi được bật, các trình giữ chỗ này được giải quyết khi gửi:

| Trình giữ chỗ | Nó làm gì |
|---|---|
| `{name}` (nhãn bất kỳ) | **Nhắc bạn điền vào** trước khi dán; **ghi nhớ giá trị lần trước** để lần sau không hỏi lại |
| `{name:John}` | Biến với **giá trị mặc định**, được điền sẵn trong lời nhắc |
| `{env\|dev\|test\|prod}` | Biến với **các lựa chọn** — lời nhắc hiển thị một danh sách thả xuống (lựa chọn đầu tiên cũng là mặc định; vẫn cho phép tự gõ) |
| `{clipboard}` | Chèn nội dung clipboard hiện tại |
| `{cursor}` | Để con trỏ ở vị trí này sau khi dán (cũng ngăn tự động Enter) |
| `{date}` `{time}` `{datetime}` | Chèn ngày/giờ hiện tại; hỗ trợ độ lệch như `{date+7}` (7 ngày sau). Các bí danh tiếng Trung `{日期}` / `{时间}` / `{日期时间}` cũng hoạt động |
| `{uuid}` `{random}` | Giá trị ngẫu nhiên: một UUID / 6 chữ số, mới mỗi lần xuất hiện |
| `{snippet:name}` | **Chèn nội dung của một đoạn văn bản khác** (sâu 3 cấp, an toàn với vòng lặp) — giữ chữ ký dùng chung ở một nơi |

Ví dụ: một chữ ký `Best regards,\n{name}` (với trình giữ chỗ được bật) → hỏi tên khi gửi → dán chữ ký đã hoàn chỉnh.

## Từ viết tắt (mở rộng khi bạn gõ, không cần bảng) · tùy chọn

Gán cho một đoạn văn bản một **từ viết tắt** để mở rộng nó trực tiếp trong bất kỳ ô nhập liệu nào. **Tiền tố kích hoạt** được đặt một lần trong Cài đặt (mặc định `;`):

1. Trong Trình quản lý, chỉ gõ **bản thân từ viết tắt** — ví dụ `sig` cho chữ ký của bạn. Tiền tố được thêm tự động (đừng gõ lại nó); trường hiển thị tiền tố hiện tại `;` ở phía trước.
2. Trong bất kỳ ô nhập liệu nào gõ **`;sig`** (= tiền tố + từ viết tắt) rồi **Space / Tab / Enter** → nó xóa `;sig` và mở rộng nội dung (một `{placeholder}` sẽ hỏi trước nếu đoạn đó có trình giữ chỗ được bật).
3. **Gõ nhầm?** Nhấn **Backspace ngay sau** khi mở rộng để hoàn nguyên nó về `;sig` (cùng cửa sổ, trong vòng 5 giây).

Chi tiết: việc khớp là **không phân biệt hoa thường** (`;SIG` vẫn kích hoạt khi CapsLock bật); một lỗi gõ được sửa bằng Backspace vẫn mở rộng; hai đoạn văn bản dùng chung một từ viết tắt sẽ nhận **cảnh báo nội dòng** trong Trình quản lý; và menu khay hệ thống có một công tắc **"Tạm dừng mở rộng"** một cú nhấp (cho việc trình diễn/chơi game).

> **Về tiền tố:** tiền tố (mặc định `;`) giúp việc gõ thông thường / bính âm không vô tình kích hoạt một từ viết tắt trần. Thay đổi nó ở **Cài đặt → Từ viết tắt → Tiền tố kích hoạt** (thành `,`, `:`, …) hoặc **để trống**. Một từ viết tắt có tiền tố tự phân định và kích hoạt ngay cả khi dính liền với văn bản phía trước (ví dụ `thx;sig`); không có tiền tố thì nó chỉ kích hoạt như một từ riêng (không bao giờ như phần đuôi của một từ dài hơn như `graf`). Bạn cũng có thể tắt từ viết tắt theo từng ứng dụng (một danh sách đen, ví dụ `cmd.exe; putty.exe`) hoặc tắt hoàn toàn.

## Đầu ra & cài đặt thông dụng (Khay → Cài đặt)

- **Đầu ra** — mặc định "dán vào ứng dụng đang hoạt động"; hoặc "chỉ sao chép vào clipboard" (bạn dán bằng `Ctrl+V`). Tùy chọn: nhấn Enter sau khi dán, nhấp một lần để gửi, khôi phục clipboard. **Mỗi đoạn văn bản có thể ghi đè điều này** (Trình quản lý → Đầu ra: theo toàn cục / dán / dán + Enter / chỉ sao chép) — câu chat tự động gửi, đoạn mã thì không bao giờ.
- **Vị trí bảng** — theo cửa sổ đang hoạt động (mặc định) / theo con trỏ văn bản / ghi nhớ vị trí lần trước.
- **Phương thức gọi ra (chọn một)** — ① **tổ hợp phím**: nhấp vào ô và nhấn một tổ hợp mới (phím thông thường cần `Ctrl`/`Alt`/`Shift`/`Win`; phím chức năng **`F1`–`F24` dùng độc lập được**); hoặc ② **gõ nhẹ một phím bổ trợ**: **gõ một hoặc hai lần một phím bổ trợ** (ví dụ `Ctrl` phải, `Shift` phải) để gọi ra (một phím bổ trợ đơn lẻ không thể là phím tắt thông thường, nên nó được phát hiện qua thao tác gõ nhẹ). Chọn gõ nhẹ sẽ tắt tổ hợp — hai cái loại trừ lẫn nhau, nên luôn rõ cái nào đang hoạt động.
- **Phím tắt thu thập** — tổ hợp thứ hai tùy chọn giúp **lặng lẽ lưu clipboard thành một đoạn văn bản mới** (phản hồi bằng bong bóng, không có cửa sổ).
- **Thư mục dữ liệu** — trỏ nó vào một ổ đĩa đồng bộ; **xuất / nhập bản sao lưu** (zip, được kiểm tra với xác nhận ghi đè); **tự động sao lưu** hàng ngày về máy này (giữ 10 bản mới nhất, truy cập thư mục bằng một cú nhấp).
- **Ngôn ngữ** — **18 ngôn ngữ**: English · 简体中文 · 繁體中文 · 日本語 · 한국어 · Español · Português · Français · Deutsch · Italiano · Русский · Tiếng Việt · ไทย · Bahasa Indonesia · हिन्दी · বাংলা · العربية (RTL) · Türkçe, chuyển đổi tức thì. **Khởi động cùng Windows** tùy chọn.
- **Kiểm tra cập nhật** — tắt theo mặc định; khi bật, ứng dụng kết nối GitHub một lần lúc khởi động để kiểm tra phiên bản mới (lần duy nhất truy cập mạng). «Kiểm tra ngay» chạy khi cần.

## Bảng tra cứu nhanh phím tắt

| Hành động | Phím |
|---|---|
| Gọi ra / đóng bảng | `Ctrl+Shift+8` (có thể cấu hình) / `Esc` |
| Chọn / chuyển danh mục / chọn nhanh | `↑↓` / `←→` / `Alt+1–9` |
| Gửi | `Enter` hoặc nhấp đúp (nhấp một lần tùy chọn) |
| Yêu thích / bỏ yêu thích | `Ctrl+D` |
| Tạo / chỉnh sửa trong bảng | `Ctrl+N` / `Ctrl+E` |
| Hoàn tác xóa (Trình quản lý) | `Ctrl+Z` |
| Từ viết tắt: kích hoạt / hoàn tác | gõ từ viết tắt + Space·Tab·Enter / Backspace sau khi mở rộng |

---

## Tính năng tổng quan

- **Gọi ra**: phím tắt toàn cục — một **tổ hợp phím** (phím chức năng dùng như phím đơn) hoặc **gõ nhẹ một/hai lần một phím bổ trợ** (ví dụ Ctrl phải), **chọn một**; bảng theo cửa sổ đang hoạt động / con trỏ văn bản / vị trí đã ghi nhớ; các nút hàng trên nhảy đến **Mới / Trình quản lý / Cài đặt**; ghim nó để gửi liên tiếp nhiều đoạn; kéo & đổi cỡ với kích thước được ghi nhớ.
- **Tìm kiếm**: tên / bính âm / chữ cái đầu / từ viết tắt / nội dung, được tô sáng; **phá vỡ đồng hạng bằng tần suất sử dụng (frecency)**; `@category keywords` giới hạn tìm kiếm vào một danh mục (chỉ `@category` để duyệt nó).
- **Nội dung**: văn bản thuần (nhiều dòng, ký tự đặc biệt, emoji không mất mát), trình giữ chỗ (giá trị mặc định / danh sách lựa chọn / lồng đoạn văn bản / uuid / random — **bật tùy chọn theo từng đoạn**), **hình ảnh** (từ clipboard hoặc tệp, được dán dưới dạng hình ảnh khi gửi; **hình ảnh cũng có thể có từ viết tắt** — gõ từ viết tắt, được hình ảnh).
- **Từ viết tắt**: kích hoạt bằng dấu kết thúc, hỏi biến, hoàn tác một cú nhấn, sửa lỗi gõ bằng Backspace, không phân biệt hoa thường, nhấp phá vỡ token, cảnh báo trùng lặp, danh sách đen theo ứng dụng, **tạm dừng bằng một cú nhấp trên khay hệ thống**.
- **Đầu ra**: dán trực tiếp / chỉ sao chép; tùy chọn tự động Enter, khôi phục clipboard, nhấp một lần để gửi; **ghi đè đầu ra theo từng đoạn**; phím tắt thu thập (clipboard → đoạn văn bản trong một cú nhấn).
- **Trình quản lý**: 7 màu danh mục, kéo sắp xếp lại / chuyển, **chọn nhiều để chuyển / xóa hàng loạt** (chọn bằng Ctrl / Shift, rồi nhấp chuột phải), hoàn tác xóa, **thùng rác (khôi phục trong 30 ngày, có xem trước nội dung)**, cảnh báo trùng từ viết tắt, thống kê sử dụng, chế độ không ngắt dòng cho mã, phản hồi khi lưu.
- **Dữ liệu**: JSON cục bộ, tải lại nóng (tự động hợp nhất chỉnh sửa từ bên ngoài / đồng bộ), thông báo xung đột đồng bộ, xuất / nhập bản sao lưu, **tự động sao lưu hàng ngày (giữ 10 bản)**, khởi động cùng Windows.
- **Bản địa hóa**: **18 ngôn ngữ giao diện** (Tiếng Trung Giản thể / Phồn thể, English, 日本語, 한국어, Español, Français, Deutsch, Русский, العربية …) với **phản chiếu phải-sang-trái cho tiếng Ả Rập**, chuyển đổi trực tiếp trong Cài đặt.
- **Độ bền bỉ**: một phiên bản duy nhất (lần khởi động thứ hai sẽ gọi bảng tìm kiếm ra thay vì cài đặt hook trùng lặp); CI chạy các bài kiểm thử cộng với kiểm tra nhanh cửa sổ ở mỗi lần push và xuất bản một exe đơn tệp trên các thẻ `v*`.

## Dữ liệu & đồng bộ

Thư mục dữ liệu (mặc định `Documents\QuickText`, có thể thay đổi trong Cài đặt, có thể trỏ vào một ổ đĩa đồng bộ):

```
<data folder>/
  ├─ index.json        # category order + each category's file name and color
  ├─ <category>.json   # the snippets in that category (Snippet[])
  ├─ trash.json        # soft-deleted snippets (auto-purged after 30 days)
  └─ images/           # image files for image snippets
```

- `Snippet`: `{ id, name, abbr, body, useVariables, outputMode, imagePath?, updatedAt }`.
- **Ghi nguyên tử** (`*.tmp` → `File.Replace`) để việc đồng bộ không bao giờ đọc một tệp ghi dở; tải lại nóng bằng `FileSystemWatcher` (được gộp lại), với một cơ chế bảo vệ chống tự-ghi.
- Trạng thái cục bộ của máy nằm **ngoài thư mục đồng bộ**: cài đặt trong `%APPDATA%\QuickText\settings.json`, số lượt dùng / mục yêu thích trong `%APPDATA%\QuickText\usage.stats` (chúng thay đổi mỗi lần gửi và sẽ xung đột giữa các máy), tự động sao lưu hàng ngày trong `%APPDATA%\QuickText\backups\`.
- **Chế độ di động** (không để lại dấu vết / USB): bật nó ở **Cài đặt → Dữ liệu → Chế độ di động** — nó đặt một tệp đánh dấu `QuickText.portable` cạnh `QuickText.exe` và **áp dụng ở lần khởi động lại tiếp theo** (lần khởi động di động đầu tiên mang theo cài đặt và số lượt dùng của bạn, nên bạn không phải cấu hình lại). Cài đặt, số lượt dùng, bản sao lưu và thư viện mặc định khi đó nằm dưới `<exe folder>\Data\` thay vì `%APPDATA%` / Documents, và "khởi động cùng Windows" dùng một shortcut trong thư mục Startup thay vì registry — nên toàn bộ công cụ di chuyển được trên một chiếc USB và không để lại gì trên máy chủ. Ứng dụng phải nằm ở một vị trí có thể ghi (một chiếc USB, không phải `Program Files`); **thư viện văn bản di chuyển qua Xuất / Nhập bản sao lưu**. Khởi động-cùng-Windows được theo dõi theo từng chế độ, nên hãy đánh dấu lại nó ở chế độ mới sau khi chuyển đổi nếu bạn muốn. Để tắt nó cho bố cục đã cài đặt ở trên (lựa chọn đúng khi thư mục dữ liệu là một ổ đĩa đồng bộ).<br>*(Nó bắt buộc phải là một tệp đánh dấu, không phải một cài đặt thông thường — nó quyết định bản thân `settings.json` nằm ở đâu; công tắc chỉ có hiệu lực ở lần khởi động tiếp theo, không bao giờ làm gián đoạn phiên hiện tại.)*

## Thương hiệu

Tài nguyên nằm trong `assets/branding/`: `quicktext-mark.svg` (chính), `quicktext-mark-mono.svg` (đơn sắc), `quicktext.ico` (16–256, biểu tượng ứng dụng / khay), `quicktext-256.png`, `quicktext-social.png` (1200×630 og:image), `quicktext-social-2x.png` (2400×1260 @2x), `brand.html` (bảng thương hiệu). Biểu tượng là một **con trỏ chèn văn bản hình chữ I** trên một ô màu xanh bạc hà bên cạnh một khối văn bản màu hổ phách vừa đáp xuống — "text▮", tức là đặt văn bản của bạn tại con trỏ. Bảng màu "terminal dusk": xanh bạc hà (màu nhấn của ứng dụng `#3DC2A0`) + hổ phách `#F2B457` trên nền mực ngả xanh mòng két; một wordmark sans kết hợp với mono Cascadia Code.

## Kiến trúc

Core thuần (không Win32, kiểm thử đơn vị được) được tách biệt khỏi Win32/UI.

| Dự án | Nội dung |
|---|---|
| `src/QuickText.Core` | `Models`, `Persistence` (`Store`, `UsageStore`, `JsonConfig`), `Search` (`SearchIndex`), `Abbr` (`AbbrMatcher`), `Snippets` (`Placeholders`), `Pinyin`, `Settings`, `Localization` (.resx, 18 ngôn ngữ) |
| `src/QuickText.App` | WPF UI (`SearchPanel` / `ManagerWindow` / `SettingsWindow` / `AppDialog` / `VariablesDialog`), `Ui/Theme.xaml` (giao diện tối), `Interop` (`GlobalHotkey`, `KeyboardHook`, `PasteEngine`, `Autostart`, `NativeMethods`) |
| `tests/QuickText.Core.Tests` | Kiểm thử đơn vị Core (xUnit) |

## Xây dựng & chạy

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App        # or run QuickText.exe under bin
```

Xuất bản một bản dựng di động đơn tệp (win-x64):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
```

Yêu cầu .NET 10 SDK. Chỉ dành cho Windows (phím tắt toàn cục Win32 / keyboard hook / clipboard).

## Về Kế hoạch 365 mã nguồn mở

Đây là dự án số 023 của [Kế hoạch 365 mã nguồn mở](https://github.com/rockbenben/365opensource).

Một người + AI, hơn 300 dự án mã nguồn mở trong một năm. [Gửi ý tưởng của bạn →](https://my.feishu.cn/share/base/form/shrcnI6y7rrmlSjbzkYXh6sjmzb)

## Giấy phép

[MIT License](../LICENSE) · Bản quyền © 2026 rockbenben. Tự do sử dụng, chỉnh sửa, và phân phối.
