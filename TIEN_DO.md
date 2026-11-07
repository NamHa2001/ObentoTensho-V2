# Tiến độ dự án Obento Tensho

Ảnh chụp trạng thái dự án: đã làm gì, chưa làm gì, đang dừng ở đâu. Cập nhật file này sau mỗi phần việc, cùng commit với phần việc đó.

Cập nhật lần cuối: 07/11/2026 — sau đợt Nhân viên + Audit của phần Quản lý khách & nhân viên.

- **Việc còn nợ chi tiết (kèm mã SRS):** `VIEC_CON_NO.md`
- **Lỗi đã gặp và cách sửa:** `NHAT_KY_LOI.md`
- **Cài đặt, chạy, cấu hình:** `README.md`
- **Yêu cầu gốc:** `docs/srs/` (chỉ có trên máy, không nằm trong repo)

---

## 1. Tổng quan

Website nhà hàng cơm Nhật một chi nhánh: đặt bàn, đặt món mang về (khách tự đến lấy, không giao hàng), thành viên tích điểm, voucher, trang quản trị. Thanh toán tiền mặt hoặc chuyển khoản do thu ngân xác nhận; không có cổng thanh toán online.

- **Repo:** `NamHa2001/ObentoTensho-V2`, thư mục `E:\Obento Tensho`, nhánh làm việc `Nam`, nhánh chính `master`
- **Backend:** .NET 10, ASP.NET Core, EF Core 10, SQL Server, FluentValidation, xunit
- **Frontend:** Angular 21 (`web/`, hai ứng dụng `customer` và `admin`) — mới là khung rỗng
- **CI:** GitHub Actions gồm backend, frontend, quét bí mật

## 2. Tiến độ theo bảng trọng số

**Toàn dự án: khoảng 61,1/100. Riêng backend: khoảng 61,1/71 (86%). Giao diện: 0/22.**

| Phần | Trọng số | Đã xong | Trạng thái |
|---|---:|---:|---|
| Khung dự án + CI | 3 | 3 | Xong |
| Nền CSDL | 3 | 3 | Xong |
| Xác thực khách | 4 | 4 | Xong |
| Xác thực nhân viên + 2FA | 3 | 3 | Xong |
| Phân quyền, audit, giới hạn tần suất, header bảo mật | 4 | 4 | Xong |
| Email + hộp thư đi | 4 | 4 | Xong |
| Bàn, ca, giờ mở cửa | 4 | 4 | Xong |
| Đặt bàn | 7 | 7 | Xong |
| Thực đơn + nhập/xuất Excel | 6 | 6 | Xong |
| Đặt món mang về | 8 | 8 | Xong |
| Điểm & hạng | 5 | 5 | Xong |
| Voucher & khuyến mãi | 5 | 5 | Xong |
| Nội dung (CMS) | 4 | 4 | Xong — ảnh, banner, gallery, tin tức, trang tĩnh, đánh giá, liên hệ, newsletter |
| Quản lý khách & nhân viên | 3 | 1,1 | **Đang làm** — xong nhân viên, step-up, xem audit; còn khách hàng, tài khoản khách |
| Báo cáo, dashboard, cài đặt | 4 | 0 | Chưa làm |
| Realtime (SignalR) | 2 | 0 | Chưa làm |
| Đa ngôn ngữ | 2 | 0 | Chưa làm |
| Giao diện khách | 12 | 0 | Chưa làm |
| Giao diện quản trị | 8 | 0 | Chưa làm |
| PWA | 2 | 0 | Chưa làm |
| Triển khai (Docker, Nginx, sao lưu) | 4 | 0 | Chưa làm |
| Kiểm thử cuối (E2E, hiệu năng, bảo mật) | 3 | 0 | Chưa làm |

Số điểm của phần đang làm dở là ước lượng. Game (Phase 2) không tính.

## 3. Thứ tự làm tiếp

Chủ dự án đã chốt: **làm hết backend rồi mới làm giao diện.**

1. **Quản lý khách & nhân viên — đang làm:** còn quản lý khách hàng, tài khoản khách
3. Báo cáo, dashboard, cài đặt
4. Realtime (SignalR)
5. Đa ngôn ngữ
6. Giao diện khách → Giao diện quản trị → PWA
7. Triển khai → Kiểm thử cuối

Chờ chủ dự án quyết định: cách xử lý thư vượt giới hạn (mục "Rủi ro thư bị bỏ" trong `VIEC_CON_NO.md`).

## 4. Đã làm theo từng phần

| Phần | Commit | Nội dung chính |
|---|---|---|
| Khung, CI | `b3fda45` → `b3d118a` | Solution 4 lớp, khung Angular, CI, ArchTests |
| Nền CSDL | `5431b1c` | EF Core, migration, `Users`, `Staff`, `RefreshTokens`, `AuditLogs`, vai trò `ot_app_runtime` |
| Xác thực | `201776c`, `fec81ab`, `ffeca06`, `7512afb` | PBKDF2, JWT hai scheme, xoay refresh token, 2FA TOTP, mã khôi phục, lời mời nhân viên |
| Phân quyền | `0b7159b` | Ma trận `StaffCapability`, audit, giới hạn tần suất theo IP thật, header bảo mật |
| Bàn và giờ | `ba39150` | Khu vực, bàn, sơ đồ, ca phục vụ, khóa ngày, giờ mở cửa |
| Email | `7f7c546` | Hộp thư đi có thử lại, SMTP/pickup, mẫu vi/en/ja, xác thực email, đặt lại mật khẩu |
| Đặt bàn | `ee0f79d`, `15532f0` | Giữ chỗ nguyên tử, tra cứu/hủy, máy trạng thái, màn hình lễ tân, OTP chọn lọc, Turnstile, việc định kỳ, tệp `.ics` |
| Thực đơn | `59d3c85`, `5d107c9` | Danh mục, món, tùy chọn, bản dịch en/ja, cache công khai, nhập/xuất Excel có xem trước |
| Đặt món | `2e76616`, `63a7973` | Giá do server tính, giờ lấy, theo dõi, tự hủy, màn hình bếp và thu ngân, hoàn tiền, cờ không đến lấy, danh sách và thư "cần xử lý" |
| Điểm & hạng | `8ee787e` | Sổ cái chỉ thêm, lô FIFO, hạng, giảm theo hạng, nợ điểm, hết hạn, điều chỉnh bốn mắt |
| Voucher bước 1 | `0539d34` | Kiểm mã, giữ lượt nguyên tử, hoàn lượt, đổi điểm, sinh hàng loạt, duyệt bốn mắt |
| Voucher bước 2 | `9da24b1` | Khuyến mãi công khai và quản trị, voucher sinh nhật, xuất Excel voucher, giới hạn kiểm mã theo tài khoản |
| CMS bước 1 | `4954ed3` | Pipeline ảnh NetVips (bản gốc, biến thể WebP, BlurHash, xử lý nền), banner hẹn ngày, gallery |
| CMS bước 2 | `18dc779` | Tin tức, danh mục tin, hẹn giờ, slug cũ 301, thùng rác 30 ngày; trang tĩnh có phiên bản |
| CMS bước 3a | `da28564` | Đánh giá sau đơn hoàn thành, duyệt và trả lời; form liên hệ qua hộp thư đi |
| CMS bước 3b | `64dc087` | Newsletter: đăng ký hai bước, hủy bằng POST và one-click, gửi theo lô, xuất danh sách |
| Nhân viên + Audit | commit ngay sau `64dc087` | Mời nhân viên, khóa/đổi vai trò, thu hồi phiên, đặt lại 2FA, step-up, đổi mật khẩu, xem nhật ký |
| Trang thử API | `1e20913` | OpenAPI + Scalar tại `/scalar/`, chỉ ở môi trường Development |

## 5. Backend hiện có

**Cấu trúc:** `Contracts` ← `Core` ← `Infrastructure` ← `Api`.

- `Contracts`: DTO, enum, mã lỗi
- `Core`: nghiệp vụ, chia thư mục `Audit`, `Auth`, `Common`, `Content`, `Loyalty`, `Media`, `Menu`, `Notifications`, `Orders`, `Reservations`, `Promotions`, `Tables`, `Time`, `Verification`, `Vouchers`
- `Infrastructure`: EF, repository, email, job, ClosedXML
- `Api`: controller mỏng

**API công khai và khách** (tiền tố `/api/v1`): `auth`, `otp`, `menu`, `banners`, `gallery`, `public/opening-hours`, `reservations`, `orders`, `promotions`, `vouchers/validate`, `account/orders`, `account/loyalty-points`, `account/vouchers`, `account/redeem-points`.

**API quản trị** (`/api/v1/admin`): `auth`, `table-areas`, `tables`, `opening-hours`, `service-slots`, `blocked-dates`, `reservations`, `menu/*`, `orders`, `customers/{id}/loyalty-points`, `loyalty/adjustments`, `vouchers`, `promotions`, `images`, `banners`, `gallery`.

**Việc định kỳ:** `reservations.reminder`, `reservations.no-show`, `reservations.pending-cutoff`, `orders.attention`, `loyalty.expire`, `loyalty.expiry-notice`, `vouchers.birthday`, `media.process-images`.

**Kiểm thử:** 969 test (406 kiến trúc, 417 đơn vị, 146 tích hợp trên SQL Server thật).

## 6. Quyết định và hành vi cần nhớ

Những điểm không thấy ngay khi đọc yêu cầu:

- **Tiền:** mọi giá do server đọc từ CSDL lúc đặt; thân request có trường lạ thì bị từ chối 400.
- **Giảm giá:** voucher và giảm theo hạng loại trừ nhau; có mã thì dùng voucher.
- **Sổ cái điểm:** chỉ thêm, tài khoản ứng dụng bị `DENY UPDATE, DELETE`. Mọi thay đổi điểm đi qua `LoyaltyLedger`, trong transaction của bên gọi.
- **Điểm tích lũy:** không giảm khi thu hồi hoặc hết hạn. Hạng không bao giờ hạ.
- **Thu hồi điểm đơn:** có mã nhưng hiện không chạy tới được, vì máy trạng thái không cho hủy đơn đã hoàn thành.
- **Sự kiện đơn hoàn thành:** cộng điểm gọi trực tiếp trong transaction, chưa dùng sự kiện outbox `OrderCompleted` như `BR-ORD-15`.
- **Tên món trong đơn:** chụp cả tiếng Việt (cho bếp) và tên theo ngôn ngữ khách, nên `OrderItems`/`OrderItemOptions` có thêm cột `*Localized` so với từ điển dữ liệu.
- **Cột thêm so với từ điển dữ liệu:** `Orders.NeedsContactConfirm`, `NoPickup`, `AttentionNotifiedAtUtc`, `CancelledByType`; `Vouchers.ApprovalStatus`, `BatchId`; bảng `LoyaltyAdjustments`.
- **Cột thêm so với từ điển dữ liệu (bước 2):** `Vouchers.BirthdayYear`, `Promotions.RowVersion`.
- **Khuyến mãi:** ngày kết thúc được bằng ngày bắt đầu (theo CHECK của từ điển dữ liệu, `BR-VCH-13` ghi "lớn hơn"). Trang công khai chỉ hiện mã dùng chung, không hiện mã cá nhân và mã sinh hàng loạt.
- **Voucher sinh nhật:** hạn dùng lấy theo `voucher.redeemExpiryDays` vì SRS chưa có khóa riêng. Sinh 29/2 nhận vào 28/2 năm thường.
- **Ảnh:** dùng NetVips, không dùng ImageSharp (`D-04`). Chỉ nhận JPEG/PNG/WebP. Ảnh ≤ 12 MP xử lý ngay trong request, lớn hơn xử lý nền mỗi phút thay vì Hangfire. Tệp nằm ở `Media:RootPath` (mặc định `App_Data/media`); máy chủ thật do Nginx phục vụ `/uploads`, API chỉ tự phục vụ khi `Media:ServeFromApi` bật (mặc định ở Development).
- **Gói gốc libvips:** chỉ cài `win-x64` và `linux-x64`. Nếu triển khai bằng ảnh Docker Alpine hoặc ARM thì phải đổi gói.
- **Quyền voucher:** dùng chung `StaffCapability.Customers` theo dòng "Khách hàng, khuyến mãi, voucher" của ma trận.
- **Giới hạn thư:** mỗi địa chỉ nhận tối đa 3 thư cùng loại mỗi giờ và 10 thư mỗi ngày; thư vượt giới hạn bị bỏ hẳn, không hoãn (xem mục "Rủi ro thư bị bỏ" của `VIEC_CON_NO.md`).
- **Định dạng liên hệ:** SĐT lưu dạng `+84…`; email lưu chữ thường.
- **Đường dẫn lạ:** trả 401 (chính sách từ chối mặc định), không phải 404.

## 7. Môi trường dev

- **CSDL:** `ObentoTensho_V2_Dev` (chạy thử), `ObentoTensho_V2_Test` (test tích hợp). Không đụng `ObentoTensho_Dev` của dự án cũ.
- **Cấu hình:** User Secrets của `src/ObentoTensho.Api`, danh sách khóa trong `README.md`.
- **Chạy API:** `dotnet run --project src/ObentoTensho.Api --launch-profile http`, cổng 5212.
- **Thử API:** `http://localhost:5212/scalar/`. Trang đã kiểm bằng lệnh, chưa mở thử trong trình duyệt.
- **Email dev:** ghi tệp `.eml` vào `src/ObentoTensho.Api/mail-pickup/`.
- **Tạo migration:** đặt `ConnectionStrings__Default` rồi chạy `dotnet ef` với `-p` và `-s` là `src/ObentoTensho.Infrastructure`.
- **Dữ liệu mẫu trong CSDL dev (không nằm trong mã nguồn, không phải dữ liệu thật):** 4 món, 2 danh mục, nhóm Size, giờ mở 10:30–21:00 mỗi ngày, 2 ca, voucher `GIAM10`.
- **Chưa có:** tài khoản nhân viên nào trong CSDL dev, nên chưa thử được API quản trị bằng tay. Tạo SuperAdmin bằng các khóa `Seed:SuperAdmin:*`, xem `README.md`.
