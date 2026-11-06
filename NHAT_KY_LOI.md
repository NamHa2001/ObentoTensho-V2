# Nhật ký lỗi và cách xử lý

Ghi lại các lỗi **đã mất công mới sửa được** trong lúc làm, theo từng phần của dự án, để lần sau gặp lại biết ngay cách xử lý. Lỗi tầm thường (gõ sai tên, thiếu dấu chấm phẩy) không cần ghi.

## Cách ghi

- Mỗi phần việc một mục (`##`); trong mục là bảng, **lỗi mới ghi lên đầu bảng**.
- Cột **Triệu chứng**: thông báo lỗi hoặc hiện tượng nhìn thấy (giữ nguyên chữ của thông báo nếu ngắn).
- Cột **Nguyên nhân**: lý do thật sự, không phải phỏng đoán. Nếu chưa chắc thì ghi rõ "chưa xác định".
- Cột **Cách sửa**: việc đã làm để hết lỗi, đủ cụ thể để làm lại (lệnh, tên file, giá trị).
- Cột **Phòng tránh**: việc nên làm để không tái phát (nếu có).
- Chỉ ghi vào sau khi **đã sửa xong và kiểm tra lại**.

---

## Hệ thống – dựng khung dự án (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Build lỗi `CA1707: Remove the underscores from member name …` ở project test | Tên test đặt theo mã yêu cầu (`AR_01_…`) có dấu gạch dưới, bộ phân tích `latest-recommended` cấm | Thêm `tests/.editorconfig` với `dotnet_diagnostic.CA1707.severity = none` (chỉ áp cho thư mục `tests/`) | Tên test dùng gạch dưới là quy ước của dự án (`docs/srs/07` TST-01); luôn đặt tắt rule này ở `tests/` |
| Build lỗi `MA0002: Use an overload that has a IEqualityComparer<string>` | Gọi `new HashSet<string>()` / `ToHashSet()` không truyền bộ so sánh chuỗi | Truyền `StringComparer.Ordinal` vào cả hai chỗ | So sánh chuỗi luôn nêu rõ `StringComparison`/`StringComparer` |
| Build lỗi `CS1012: Too many characters in character literal` ở dòng `Replace('\\', '/')` | Tạo file bằng heredoc của shell: ký tự gạch chéo ngược bị nuốt mất một dấu, nên literal ký tự thành `'\'` (sai) | Sửa lại dòng bằng công cụ chỉnh sửa file để có đủ hai dấu gạch chéo ngược `'\\'` | Không tạo file C# có gạch chéo ngược bằng heredoc/python trong shell; dùng công cụ ghi file trực tiếp rồi build ngay |
| Bộ cài `winget install Microsoft.DotNet.SDK.10` chạy mãi, không báo lỗi | Bộ cài đang chờ hộp thoại quyền quản trị (UAC) trên màn hình, chưa ai bấm đồng ý | Bấm **Yes** ở hộp thoại UAC; kiểm lại bằng `dotnet --list-sdks` thấy `10.0.403` | Báo trước cho người dùng khi lệnh cần quyền quản trị; không đợi vô hạn |
| `ng new` / `ng generate` sinh thêm file thừa: `.vscode/`, `README.md`, `.gitignore`, `.editorconfig` trong `web/` | Mặc định của Angular CLI | Xóa các file đó (repo đã có `.gitignore` và `.editorconfig` ở gốc) trước khi commit | Sau mỗi lần chạy CLI sinh mã, chạy `git status` và xóa file thừa trước khi `git add` |
