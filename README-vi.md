# Windows Tray Invert

[English](README.md) | Tiếng Việt

Windows 11 đưa phần lớn icon mới ở khay hệ thống (tray) vào menu biểu tượng ẩn, nên bạn phải vào Settings bật từng cái.
Windows Tray Invert đảo lại quy tắc đó: **mọi icon tray đều hiện trên taskbar**, chỉ những app bạn chọn mới vào menu biểu tượng ẩn.

## Tính năng

- Mặc định hiện mọi icon tray, kể cả icon của app cài sau này.
- Danh sách từng app (icon, tên, đường dẫn) kèm công tắc để hiện icon trên taskbar hoặc đưa vào menu biểu tượng ẩn.
- Hiện tất cả / ẩn tất cả các app đang liệt kê (sau khi lọc tìm kiếm), có hỏi xác nhận.
- Tìm theo tên hoặc đường dẫn; mở thư mục của app ngay trên từng dòng.
- Dọn mục mồ côi: bản ghi icon tray có file exe không còn tồn tại. Bạn chọn mục nào cần xoá.
- Khởi động cùng Windows (chạy ẩn dưới tray).
- Giao diện Windows 11 (Mica), theme Hệ thống / Sáng / Tối, tiếng Anh và tiếng Việt.
- Chỉ chạy một bản: mở lại app thì cửa sổ của bản đang chạy hiện lên.
- Không cần quyền quản trị.

## Cài đặt

1. Tải file zip ở [Releases](../../releases):
   - `...-win-x64-standalone.zip`: có sẵn .NET runtime (không chắc thì chọn bản này);
   - `...-win-x64-needs-dotnet8.zip`: nhẹ hơn, cần cài [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Giải nén ra đâu cũng được rồi chạy `WindowsTrayInvert.exe`.
3. Bật **Khởi động cùng Windows** nếu muốn app tiếp tục làm việc sau khi khởi động lại máy.

Cần Windows 11 (app dùng cài đặt icon tray mà Windows 11 lưu cho từng app).

## Cách hoạt động

Windows lưu mỗi icon tray thành một bản ghi ở `HKCU\Control Panel\NotifyIconSettings\<id>`. Giá trị `IsPromoted` quyết định icon nằm đâu: `1` = trên taskbar, `0` hoặc không có = menu biểu tượng ẩn. Explorer theo dõi key này và áp dụng thay đổi trong khoảng một giây, không cần khởi động lại.

App theo dõi cùng key đó. Mỗi khi có bản ghi được thêm hoặc đổi, app đặt `IsPromoted` theo danh sách của bạn: `1`, trừ khi exe của app nằm trong danh sách ẩn. App chỉ ghi những giá trị đang khác.

Lưu ý:
- Danh sách ẩn khớp theo đường dẫn exe. App đặt nhiều icon lên tray từ cùng một exe (ví dụ `nvcontainer.exe` của NVIDIA) dùng chung một cài đặt.
- Đổi icon trong Windows Settings khi app đang chạy sẽ bị app ghi đè theo danh sách của nó; hãy đổi trong app.
- Xoá mục mồ côi là xoá bản ghi registry của nó. Nếu app đó lại hiện icon tray thì Windows tạo bản ghi mới.

## Cài đặt của app

Lưu ở `%AppData%\WindowsTrayInvert\config.json` (danh sách ẩn, theme, ngôn ngữ). Khởi động cùng Windows là giá trị `WindowsTrayInvert` ở `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Gỡ bỏ: tắt Khởi động cùng Windows, thoát từ menu tray, rồi xoá thư mục app và `%AppData%\WindowsTrayInvert`.

## Build

Cần .NET 8 SDK (hoặc mới hơn) trên Windows.

```
dotnet build src/WindowsTrayInvert.slnx -c Release
```

Version lấy từ tag git `vM.N.P` gần nhất (commit sau tag ra `M.N.P-dev.K`).

## Phát hành

Gắn tag `vM.N.P` lên một commit của `master` rồi push tag; GitHub Actions ([release.yml](.github/workflows/release.yml)) build hai file zip và tạo GitHub Release, ghi chú gồm các commit kể từ tag trước:

```
git tag v1.0.0
git push origin v1.0.0
```

## Giấy phép

[MIT](LICENSE)
