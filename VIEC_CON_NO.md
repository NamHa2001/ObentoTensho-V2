# Việc còn nợ

Những việc đã cố ý để lại khi làm từng phần (kèm mã yêu cầu trong SRS). Xong việc nào thì gạch khỏi đây trong cùng commit.

Cập nhật lần cuối: 07/11/2026 — sau phần Đặt bàn.

## Xác thực & tài khoản

| Việc | Mã | Ghi chú |
|---|---|---|
| Khách đổi mật khẩu khi đang đăng nhập | `FR-AUTH-06` | Cần mật khẩu hiện tại, thu hồi phiên khác |
| Hồ sơ khách: xem/sửa tên, SĐT, ngày sinh, ảnh đại diện | `FR-AUTH-08`, `BR-LOY-20` | Ảnh qua pipeline ảnh (`04`) |
| Đổi email (xác minh email mới, báo email cũ) | `BR-AUTH-15` | |
| Trang "Thiết bị đăng nhập", "Đăng xuất mọi thiết bị" | `FR-AUTH-09`, `SEC-52` | Đã giới hạn 5 phiên; chưa có API liệt kê/thu hồi |
| Chặn email dùng một lần khi nhận ưu đãi | `BR-AUTH-16` | Làm cùng phần voucher |
| Gắn đơn/đặt bàn vãng lai vào tài khoản | `BR-AUTH-17` | |
| Client native: refresh token trong body (`X-Client: native`) | `BR-AUTH-12` | Phase 3, API nên sẵn |
| Denylist `jti` khi đăng xuất | `SEC-50` | Hiện access token còn dùng được tối đa 15 phút sau đăng xuất (nếu security stamp không đổi) |
| Lockout theo cửa sổ 15 phút thật, trễ tăng dần, CAPTCHA sau vài lần sai, email cảnh báo, khóa theo (tài khoản + IP) | `BR-AUTH-03`, `SEC-51` | Hiện: bộ đếm chỉ về 0 khi đăng nhập đúng hoặc khi bị khóa |
| Danh sách mật khẩu phổ biến lớn hơn (~80 mục hiện tại) | `BR-AUTH-01` | Thay tệp `common-passwords.txt`, không cần sửa mã |
| Cảnh báo đăng nhập admin từ thiết bị/IP mới | `SEC-58`, email #23/#29 | |

## Nhân viên

| Việc | Mã | Ghi chú |
|---|---|---|
| Quản lý nhân viên: tạo + gửi lời mời, gửi lại lời mời, khóa/mở, đổi vai trò, thu hồi phiên, đặt lại 2FA | `18` §API, `BR-STF-*` | Hiện chỉ có luồng nhận lời mời; chưa có API tạo lời mời |
| Không khóa/hạ quyền SuperAdmin cuối cùng | `BR-STF-04` | |
| Xác thực lại (step-up) cho thao tác rủi ro cao | `SEC-61` | Đổi `payment.*`, đổi vai trò, xuất khách, tắt 2FA… |
| Nhân viên đổi mật khẩu khi đang đăng nhập | `FR-AUTH-06` | Hiện chỉ có đổi mật khẩu bắt buộc lúc đăng nhập |
| Bật 2FA tự nguyện cho vai trò không bắt buộc; tạo lại mã khôi phục | `2fa/recovery` | |
| API `GET /admin/me` trả vai trò + danh sách quyền cho menu | `01` §3 | |
| "Màn hình ca" bằng PIN cho bếp/thu ngân | `SEC-74` | |

## Bảo mật & vận hành

| Việc | Mã | Ghi chú |
|---|---|---|
| Bộ đếm rate limit, `IActionThrottle`, cache security stamp, cache giờ mở cửa đang trong bộ nhớ từng tiến trình → Redis/HybridCache khi chạy nhiều instance | `09` §6, `PERF-11` | |
| Log có cấu trúc (Serilog), che dữ liệu nhạy cảm trong log, chống log injection | `SEC-53` | |
| `/health/ready` (SQL, SMTP…) | `02` | Hiện chỉ có `/health/live` |
| OpenAPI 3.1 + client TypeScript sinh tự động, Swagger chỉ Dev/Staging | `FR-ARC-03`, `09` §1 | |
| `/.well-known/security.txt`, runbook sự cố | `SEC-60` | |
| Dọn dữ liệu theo vòng đời: outbox đã xử lý 7 ngày, `OtpChallenges` 1 ngày, `EmailLogs` 90 ngày, tài khoản chưa xác thực 30 ngày, refresh token hết hạn > 30 ngày | `DB-13` | Thêm `IRecurringJob` |
| Việc định kỳ chạy theo giờ VN cố định (08:00, 01:00…) | `20` §4 | Bộ chạy hiện chỉ theo chu kỳ; cần cho sinh nhật, hết hạn điểm |
| Bảng cài đặt `SystemSettings` + trang Cài đặt | `19` §3 | Hiện `reservation.*`, `email.dailyQuota`, `otp.*` đọc từ cấu hình |
| Turnstile khi Cloudflare lỗi: đang từ chối hẳn; SRS gợi ý "chặt hơn bằng rate limit" | `SEC-57` | Cân nhắc lại khi triển khai |
| Tách migration bật RCSI thành migration riêng | `DB-09` | Tùy chọn |
| Lưu khóa Data Protection bền + mã hóa bằng chứng chỉ ở máy chủ | `SEC-16` | Bắt buộc khi triển khai (mất khóa ⇒ mất 2FA) |
| Hai SuperAdmin cùng sửa giờ mở cửa thường kỳ đúng cùng lúc có thể cùng qua kiểm ETag | `BR-TBL-16` | Rủi ro nhỏ; khóa bằng transaction nếu cần |

## Email & thông báo

| Việc | Mã | Ghi chú |
|---|---|---|
| Trang quản trị: hàng thư thất bại, thống kê hạn mức, nút "Gửi email thử" | `BR-NTF-01`, `BR-NTF-07`, `BR-NTF-12` | |
| Cảnh báo Manager khi dùng ≥ 80% hạn mức email | `BR-NTF-12` | |
| Xử lý bounce/complaint (ngừng gửi địa chỉ bounce cứng) | `BR-NTF-11` | Webhook của nhà cung cấp |
| Thư nội bộ: đặt bàn mới cho lễ tân (#3), khách hủy (#7), liên hệ mới (#19), cảnh báo quản trị (#29), tóm tắt việc cần xử lý (#30) | `20` §2, `BR-NTF-09` | |
| Các thư còn lại theo danh mục | `20` §2 | Đơn hàng, điểm, voucher, newsletter… làm cùng phần tương ứng |

## Bàn & giờ mở cửa

| Việc | Mã | Ghi chú |
|---|---|---|
| Ghép bàn | `BR-TBL-09` | Phase 2 |
| Trạng thái bàn real-time trên sơ đồ | `RT-05` | Phần realtime |
| Tự đặt bàn về "Đã đặt" đầu ngày cho đặt bàn đã xác nhận hôm nay | `BR-RSV-09` | Hiện chỉ đồng bộ khi đổi trạng thái/gán bàn |

## Đặt bàn

| Việc | Mã | Ghi chú |
|---|---|---|
| Cộng điểm khi check-in (một lần, theo `ReferenceId`) + sự kiện `ReservationCheckedIn` | `BR-RSV-07`, `BR-RSV-14` | Phần điểm/hạng |
| Nhắc nhân viên khi `Pending` quá 30 phút, báo Manager | `BR-RSV-25` | Phần thông báo nội bộ/realtime |
| Chỗ trống cập nhật real-time; báo lễ tân `reservation.created` | `RT-01` | Phần realtime |
| Khách xem "Đặt bàn của tôi" trong tài khoản | `FR-RSV-03` | Hủy từ tài khoản đã có; còn API danh sách |
| Núm `reservation.cancellationHours` | `BR-RSV-06` | Mặc định 0 (hủy tự do) nên chưa cần |

## Thực đơn

| Việc | Mã | Ghi chú |
|---|---|---|
| Nhập/xuất Excel món (xem trước, tất cả hoặc không, ≤ 1000 dòng / 2 MB, chặn công thức) | `BR-MNU-10`, `BR-MNU-14` | Bước 2 của phần thực đơn — làm ngay sau commit này |
| Ảnh món, ảnh danh mục (`MainImageId`, nhiều ảnh/món) | `BR-MNU-06`, `BR-MNU-12` | Cần pipeline ảnh (bảng `Images`, xử lý ImageSharp, WebP) |
| `GET /menu/items/{slug}/reviews` | `docs/srs/15` §4 | Làm cùng phần đánh giá sau đơn hàng |
| Xóa tùy chọn đã có trong đơn ⇒ chỉ được ẩn | `BR-MNU-23` | Hiện xóa thẳng; thêm kiểm khi có bảng `OrderItemOptions` |
| Màn hình tiến độ dịch (bao nhiêu mục thiếu/lỗi thời theo ngôn ngữ) | `docs/srs/22` | Phần đa ngôn ngữ |
| Cache thực đơn nhiều instance (HybridCache + Redis, xóa cache phát tán) | `PERF-10` | Hiện `IMemoryCache` một tiến trình; phần triển khai |
| Test đếm truy vấn `GET /menu/items` ≤ 2 | `PERF-51`, `TST-28` | Thêm interceptor đếm truy vấn ở phần kiểm thử cuối |

## Chưa bắt đầu (theo thứ tự dự kiến)

Import thực đơn (bước 2) → Đặt món mang về → Điểm & hạng → Voucher & khuyến mãi → Nội dung (CMS) → Quản lý khách & nhân viên → Báo cáo, dashboard, cài đặt → Realtime (SignalR) → Đa ngôn ngữ → Giao diện khách → Giao diện quản trị → PWA → Triển khai (Docker, Nginx, Redis, sao lưu, Cloudflare) → Kiểm thử cuối (E2E, hiệu năng, bảo mật).

## Việc của chủ dự án

- Nhập giờ mở cửa và ca phục vụ thật (CSDL đang để "đóng cửa" cả 7 ngày).
- Điền chỗ trống trong các trang pháp lý; quyết định D-23, D-30; người dịch.
- Khi triển khai: tài khoản gửi email (SPF/DKIM/DMARC), khóa Cloudflare Turnstile, tên miền.
