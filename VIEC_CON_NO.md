# Việc còn nợ

Những việc đã cố ý để lại khi làm từng phần (kèm mã yêu cầu trong SRS). Xong việc nào thì gạch khỏi đây trong cùng commit.

Tổng quan đã làm/chưa làm: xem `TIEN_DO.md`.

Cập nhật lần cuối: 07/11/2026 — sau bước 1 phần Nội dung (CMS).

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

## Rủi ro thư bị bỏ

| Việc | Mã | Ghi chú |
|---|---|---|
| Thư vượt giới hạn theo người nhận bị bỏ hẳn: thư gộp "cần xử lý" (`order_attention`) đã đánh dấu đơn "đã báo" nên đơn có thể không bao giờ được báo qua thư; khách đặt nhiều đơn trong giờ có thể mất thư xác nhận | `BR-NTF-08`, `docs/srs/20` #30 | Phát hiện khi tổng hợp tài liệu, chưa có test. Cần quyết định: miễn giới hạn cho thư giao dịch/vận hành, hoặc đổi sang hoãn. Danh sách `GET /admin/orders/attention` không bị ảnh hưởng |

## Bàn & giờ mở cửa

| Việc | Mã | Ghi chú |
|---|---|---|
| Ghép bàn | `BR-TBL-09` | Phase 2 |
| Trạng thái bàn real-time trên sơ đồ | `RT-05` | Phần realtime |
| Tự đặt bàn về "Đã đặt" đầu ngày cho đặt bàn đã xác nhận hôm nay | `BR-RSV-09` | Hiện chỉ đồng bộ khi đổi trạng thái/gán bàn |

## Đặt bàn

| Việc | Mã | Ghi chú |
|---|---|---|
| Nhắc nhân viên khi `Pending` quá 30 phút, báo Manager | `BR-RSV-25` | Phần thông báo nội bộ/realtime |
| Chỗ trống cập nhật real-time; báo lễ tân `reservation.created` | `RT-01` | Phần realtime |
| Khách xem "Đặt bàn của tôi" trong tài khoản | `FR-RSV-03` | Hủy từ tài khoản đã có; còn API danh sách |
| Núm `reservation.cancellationHours` | `BR-RSV-06` | Mặc định 0 (hủy tự do) nên chưa cần |

## Thực đơn

| Việc | Mã | Ghi chú |
|---|---|---|
| Nhập/xuất Excel: thêm cột nhóm tùy chọn, mô tả đầy đủ; nhập danh mục | `BR-MNU-14` | Bản hiện tại chỉ món (giữ nguyên các phần đó khi cập nhật) |
| Chống zip bomb khai sai kích thước giải nén trong header | `SEC-11`, `BR-MNU-14` | Hiện kiểm tổng `Length` khai trong zip (≤ 30 MB) + tệp ≤ 2 MB + chỉ Manager/SuperAdmin; có thể thêm đọc thử từng entry có giới hạn |
| Ảnh món, ảnh danh mục (`MainImageId`, nhiều ảnh/món) | `BR-MNU-06`, `BR-MNU-12` | Pipeline ảnh đã có (`Images`); còn gắn cột `MainImageId`, `MenuItemImages`, `MenuCategories.ImageId` và DTO |
| `GET /menu/items/{slug}/reviews` | `docs/srs/15` §4 | Làm cùng phần đánh giá sau đơn hàng |
| Xóa tùy chọn đã có trong đơn ⇒ chỉ được ẩn | `BR-MNU-23` | Hiện vẫn xóa thẳng (đơn cũ giữ bản chụp nhờ `OptionId` SET NULL). Bảng `OrderItemOptions` đã có nên làm được rồi |
| Màn hình tiến độ dịch (bao nhiêu mục thiếu/lỗi thời theo ngôn ngữ) | `docs/srs/22` | Phần đa ngôn ngữ |
| Cache thực đơn nhiều instance (HybridCache + Redis, xóa cache phát tán) | `PERF-10` | Hiện `IMemoryCache` một tiến trình; phần triển khai |
| Test đếm truy vấn `GET /menu/items` ≤ 2 | `PERF-51`, `TST-28` | Thêm interceptor đếm truy vấn ở phần kiểm thử cuối |

## Voucher & khuyến mãi

| Việc | Mã | Ghi chú |
|---|---|---|
| Banner khuyến mãi (`BannerImageId`) | `docs/srs/08` §6 | Pipeline ảnh đã có; còn gắn cột và DTO |
| Bản dịch en/ja của khuyến mãi | `D-36`, `docs/srs/22` | Phần đa ngôn ngữ |
| Tắt tự động khuyến mãi hết hạn | `docs/srs/14` §3 | Không cần: hiệu lực tính khi truy vấn, job chỉ là dọn dẹp tùy chọn |
| Voucher sinh nhật có thể bị lợi dụng bằng cách đăng ký mới với ngày sinh là hôm nay | `BR-LOY-20` | SRS chỉ khóa việc sửa ngày sinh; cần chủ dự án quyết có yêu cầu tài khoản đủ cũ không. Hiện chỉ yêu cầu email đã xác thực |
| Sửa ngày sinh một lần, khóa 60 ngày quanh sinh nhật | `BR-LOY-20` | Làm cùng hồ sơ khách; hiện ngày sinh chỉ nhập lúc đăng ký |
| Voucher giới hạn theo SĐT/email cho khách vãng lai | `D-16` | Đã chốt bắt buộc đăng nhập; không làm |

## Điểm & hạng

| Việc | Mã | Ghi chú |
|---|---|---|
| Đối soát hằng đêm `CurrentPoints = Σ sổ cái = Σ lô` và cảnh báo khi lệch | `BR-LOY-04` | Bất biến đã giữ trong mọi thao tác; còn thiếu việc định kỳ kiểm lại |
| Cảnh báo nhân viên khi khách có nợ điểm | `BR-LOY-08` | Số nợ đã hiện ở `GET /admin/customers/{id}/loyalty-points`; màn hình ở giao diện quản trị |
| Đọc ngưỡng hạng/thang điểm từ bảng cài đặt (hiện từ cấu hình `Loyalty:*`) | `BR-LOY-03`, `BR-RPT-21` | Phần cài đặt |
| Ẩn danh hóa khách giữ nguyên sổ cái | `BR-CUS-05` | Phần quản lý khách |
| Điểm/vé chơi game dùng chung sổ cái | `BR-LOY-13` | Phase 2 |

## Đặt món mang về

| Việc | Mã | Ghi chú |
|---|---|---|
| Nhắc "cần xử lý" real-time cho thu ngân (hiện qua thư gộp cho quản lý mỗi 10 phút + `GET /admin/orders/attention`) | `BR-ORD-18`, `docs/srs/20` #30 | Phần realtime |
| Ngôn ngữ thư cho nhân viên (hiện luôn tiếng Việt vì bảng Staff chưa có cột ngôn ngữ) | `docs/srs/22` | Phần đa ngôn ngữ |
| STK + QR chuyển khoản lấy từ cài đặt `payment.*` | `FR-ORD-03` | Phần cài đặt |
| Đơn mới/đổi trạng thái real-time cho bếp, thu ngân, trang theo dõi của khách | `RT-03`, `FR-ORD-05` | Phần realtime |
| Turnstile chỉ bắt buộc với khách vãng lai | `BR-ORD-20` | Hiện bộ lọc Turnstile áp cho mọi lượt đặt khi bật |
| Đánh giá món sau khi nhận đơn (`POST /reviews`) | `BR-CNT-11` | Phần nội dung/đánh giá |
| Gộp giỏ `localStorage` khi đăng nhập, kiểm lại giá khi mở giỏ | `FR-ORD-01` | Giao diện khách (API `POST /orders/quote` đã có) |

## Nội dung (CMS)

| Việc | Mã | Ghi chú |
|---|---|---|
| Slug cũ 301 cho món và khuyến mãi | `BR-CNT-41` | Bảng `SlugRedirects` đã có; hiện mới dùng cho tin tức |
| Đổi `Location` 301 từ đường dẫn API sang đường dẫn trang khi làm SSR | `BR-CNT-41` | Làm ở giao diện khách |
| Đưa nội dung `docs/legal/` vào trang tĩnh sau khi chủ quán duyệt | `FR-CNT-04` | Trang chưa có nội dung trả 404 |
| Bản dịch en/ja tin tức, trang tĩnh | `docs/srs/22` | Phần đa ngôn ngữ |
| Newsletter | `FR-CNT-07` | Đang làm tiếp |
| Báo real-time `contact.created` cho quản lý | `FR-CNT-06` | Làm ở phần realtime; hiện đã có email nội bộ và danh sách chưa đọc |
| Thư báo tin liên hệ nội bộ cũng chịu giới hạn 3 thư cùng loại/giờ theo địa chỉ nhận, thư vượt bị bỏ | `BR-CNT-33`, `BR-NTF-08` | Tin vẫn nằm trong danh sách chưa đọc; gộp chung quyết định về thư bị bỏ |
| Cache danh sách đánh giá công khai | `PERF-11/12` | Hiện đọc thẳng DB, có phân trang và chỉ mục |
| Dọn ảnh mồ côi sau 30 ngày, xóa gốc + biến thể | `BR-CNT-32`, `FR-IMG-10` | Hiện xóa banner/gallery thì ảnh ở lại |
| Sinh lại biến thể khi đổi cấu hình (tăng `Version`), AVIF | `FR-IMG-03/04` | `Version` đã có; chưa có lệnh sinh lại |
| Ảnh nền bị `Failed` thì chưa có cách thử lại hay cảnh báo | `FR-IMG-08` | Ảnh gốc vẫn còn |
| Làm nét nhẹ khi thu nhỏ ảnh món | `FR-IMG-07` | Tùy chọn |
| Giới hạn tốc độ tải dữ liệu chậm | `SEC-76` | Làm ở phần triển khai |
| Bản dịch en/ja banner, gallery | `docs/srs/22` | Phần đa ngôn ngữ |
| CSP `frame-src` cho YouTube/TikTok | `BR-CNT-35`, `SEC-09` | Làm ở giao diện khách |
| Kiểm giấy phép `SixLabors.Fonts` (gói phụ thuộc gián tiếp của ClosedXML) | `D-04` | `D-04` tránh ImageSharp vì giấy phép Six Labors; gói này cùng hãng, chưa kiểm điều khoản |

## Chưa bắt đầu (theo thứ tự dự kiến)

Làm hết backend rồi mới làm giao diện (chủ dự án đã chốt).

Nội dung (CMS, bước 2, 3) → Quản lý khách & nhân viên → Báo cáo, dashboard, cài đặt → Realtime (SignalR) → Đa ngôn ngữ → Giao diện khách → Giao diện quản trị → PWA → Triển khai (Docker, Nginx, Redis, sao lưu, Cloudflare) → Kiểm thử cuối (E2E, hiệu năng, bảo mật).

## Việc của chủ dự án

- Nhập giờ mở cửa và ca phục vụ thật. CSDL mới mặc định "đóng cửa" cả 7 ngày; CSDL dev đang có giờ mẫu, không phải giờ thật.
- Xem lại thang điểm và hạng (`D-34`): các con số hiện là mặc định tạm.
- Điền chỗ trống trong các trang pháp lý; quyết định D-23, D-30; người dịch.
- Khi triển khai: tài khoản gửi email (SPF/DKIM/DMARC), khóa Cloudflare Turnstile, tên miền.
