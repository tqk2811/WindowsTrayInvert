# Kế hoạch WindowsTrayInvert

> Ngày ghi: 2026-11-05

## 1. Mục tiêu

Windows 11 chỉ cho bật toggle để **hiện** từng icon lên tray; icon mới mặc định bị ẩn vào menu ẩn.
WindowsTrayInvert đảo ngược cơ chế này:

- Mọi icon **mặc định hiện** trên tray.
- Chỉ những app nằm trong **danh sách ẩn** do người dùng chọn mới bị đưa vào menu ẩn.

## 2. Cơ chế của Windows

- Mỗi icon có một key: `HKCU\Control Panel\NotifyIconSettings\<id>`.
- Các giá trị quan trọng:

| Giá trị | Kiểu | Ý nghĩa |
|---|---|---|
| `IsPromoted` | DWORD | `1` = hiện trên tray, `0`/không có = ẩn |
| `ExecutablePath` | String | Đường dẫn exe sở hữu icon (đôi khi dạng `{KnownFolderGUID}\...`) |
| `InitialTooltip` | String | Tooltip ban đầu, dùng để hiển thị tên |
| `IconSnapshot` | Binary | Ảnh icon (có thể dùng cho UI) |

- Cấu trúc này **không được Microsoft công bố chính thức**, có thể đổi theo bản cập nhật Windows.

## 3. Thiết kế

Luồng chính: `RegistryWatcher` phát hiện thay đổi → `PromotionEnforcer` đọc `HideListConfig` rồi ghi `IsPromoted`; `TrayUI` sửa `HideListConfig`.

- **RegistryWatcher**: theo dõi `NotifyIconSettings` bằng `RegNotifyChangeKeyValue` (theo dõi cả subtree,
  cả việc thêm key lẫn đổi giá trị).
- **PromotionEnforcer**: duyệt mọi subkey; app trong danh sách ẩn thì đặt `IsPromoted = 0`, còn lại đặt `1`.
  Chỉ ghi khi giá trị khác, tránh vòng lặp vô hạn do chính lần ghi kích hoạt watcher.
- **HideListConfig**: danh sách ẩn lưu JSON ở `%AppData%\WindowsTrayInvert\config.json`, so khớp theo
  `ExecutablePath` (không phân biệt hoa thường, đã chuẩn hoá known folder GUID).
- **OrphanCleaner**: dọn các key mồ côi, tức `ExecutablePath` (sau khi chuẩn hoá known folder GUID) không còn
  tồn tại trên đĩa (vd bản cũ của app đã gỡ/cập nhật). Chỉ chạy khi người dùng bấm "Dọn…": hộp xác nhận liệt kê
  từng mục kèm checkbox (mặc định tick, có thanh cuộn khi dài), chỉ xoá mục được tick; bỏ qua path trên ổ mạng,
  ổ chưa sẵn sàng (USB rút ra) hoặc known folder chưa giải được để không xoá nhầm. Mục mồ côi có nhãn cảnh báo trong danh sách.
  Xoá cả subkey `NotifyIconSettings\<id>`.
- **TrayUI**: icon riêng của app trên tray, menu chuột phải liệt kê các icon đang biết kèm checkbox "Ẩn",
  mục "Khởi động cùng Windows", "Thoát".

## 4. Công nghệ

- C# .NET 8, WPF (cửa sổ quản lý) + WinForms chỉ để dùng `NotifyIcon` (WPF không có tray icon); không cần quyền admin vì chỉ ghi `HKCU`.
- Khởi động cùng Windows qua `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Một instance duy nhất (named mutex).

## 5. Các bước

1. **Prototype (console)**: đặt `IsPromoted = 1` cho một app đang ẩn, kiểm tra Explorer có áp dụng ngay không.
   - Không áp dụng ngay: thử gửi broadcast `WM_SETTINGCHANGE`, hoặc ghi nhận phải chờ icon đăng ký lại / restart `explorer.exe`.
2. **Core**: RegistryWatcher + PromotionEnforcer + HideListConfig, chạy được dạng console.
3. **TrayUI**: menu quản lý danh sách ẩn, autostart, single instance.
   - Mục "Dọn icon mồ côi…": mở danh sách key có `ExecutablePath` không tồn tại, xác nhận rồi xoá (OrphanCleaner).
4. **Hoàn thiện**: log lỗi ra file, README, CI build release.

## 6. Rủi ro và câu hỏi mở

- ~~Explorer có thể cache trạng thái~~ Đã kiểm (2026-11-05, Win11 26300): ghi `IsPromoted` bằng registry
  (`Set-ItemProperty`, không quyền admin, không broadcast gì) thì icon hiện/ẩn ngay trong ~1,5 giây.
  Explorer tự theo dõi key `NotifyIconSettings`; Settings cũng chỉ ghi registry. Không cần `WM_SETTINGCHANGE`.
- Người dùng bật/tắt toggle trong Settings sẽ bị app ghi đè; cân nhắc tự đồng bộ thay đổi đó vào danh sách ẩn.
- Một số icon dùng cùng exe (vd nhiều icon của một app) không phân biệt được chỉ bằng `ExecutablePath`;
  có thể phải so khớp thêm theo `<id>` của key.
- Cấu trúc registry đổi theo phiên bản Windows: cần kiểm tra trên 23H2/24H2/25H2.
