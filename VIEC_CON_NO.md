# Việc còn nợ

Những việc đã cố ý để lại khi làm từng phần (kèm mã yêu cầu trong SRS) và **đề xuất cách xử lý** cho từng việc. Xong việc nào thì gạch khỏi đây trong cùng commit.

Tổng quan đã làm/chưa làm: xem `TIEN_DO.md`.

Cập nhật lần cuối: 08/11/2026 — sau phần Đa ngôn ngữ (backend).

**Cách đọc:**

- Mục 1 là các việc **chủ dự án phải quyết**: có lựa chọn, đề xuất của người làm và cách trả lời.
- Mục 2 là **thứ tự ưu tiên** theo mức độ ảnh hưởng tới an toàn và vận hành.
- Từ mục 3 trở đi là chi tiết theo module. Cột **Đề xuất** là cách người làm sẽ làm nếu không có chỉ đạo khác.

---

## 1. Cần chủ dự án quyết định

Trả lời ngắn theo mã, ví dụ: "Q1 đồng ý đề xuất, Q2 chọn QuestPDF". Mục nào chưa trả lời thì giữ nguyên cách đang chạy.

| Mã | Vấn đề | Lựa chọn | Đề xuất của người làm | Chủ dự án cần làm gì |
|---|---|---|---|---|
| Q1 | **Thư vượt giới hạn bị bỏ hẳn** (`BR-NTF-08`): khách đặt nhiều đơn trong một giờ có thể không nhận được thư xác nhận; thư nội bộ "cần xử lý" có thể không bao giờ tới | (a) Giữ nguyên: bỏ thư vượt giới hạn. (b) Chia nhóm thư như cột bên. (c) Hoãn mọi thư vượt giới hạn sang giờ sau | **(b)**, theo đúng tinh thần `BR-NTF-09`. Thư giao dịch gửi cho chính chủ (xác nhận đơn/đặt bàn, OTP, đặt lại mật khẩu, đổi email) không áp giới hạn 3 thư/giờ, chỉ giữ trần tuyệt đối 30 thư/ngày/địa chỉ để chống spam. Thư nội bộ gộp thành bản tóm tắt, không bỏ; đơn chỉ được đánh dấu "đã báo" khi thư đã thực sự gửi. Thư quảng cáo (bản tin) giữ giới hạn và dời sang ngày sau | Trả lời "Q1 đồng ý" hoặc chọn (a)/(c). Đây là quyết định nghiệp vụ, không cần kiến thức kỹ thuật |
| Q2 | **Thư viện xuất PDF báo cáo** (`D-04`, `BR-RPT-13`). Hiện `format=pdf` trả 400 | (a) QuestPDF: gọn, nhanh, không cần cài thêm gì trên máy chủ. (b) Playwright: giấy phép tự do nhưng phải cài trình duyệt Chromium lên máy chủ, ảnh Docker nặng thêm vài trăm MB, tốn RAM khi xuất. (c) Chưa cần PDF, chỉ dùng Excel | **(a) QuestPDF.** Theo điều khoản người làm biết, giấy phép Community miễn phí cho doanh nghiệp có doanh thu năm dưới 1 triệu USD (khoảng 25 tỷ đồng). Một quán một chi nhánh gần như chắc chắn thuộc diện này | Mở trang giấy phép QuestPDF (questpdf.com, mục License), xác nhận điều khoản hiện hành và doanh thu năm của quán dưới ngưỡng, rồi trả lời "Q2 chọn QuestPDF". Nếu không muốn phụ thuộc điều kiện doanh thu thì chọn (b) hoặc (c) |
| Q3 | **Lợi dụng voucher sinh nhật** (`BR-LOY-20`): tạo tài khoản mới, khai ngày sinh là hôm nay để nhận voucher ngay. SRS chỉ khóa việc *sửa* ngày sinh | (a) Giữ nguyên: chỉ cần email đã xác thực. (b) Thêm điều kiện như cột bên | **(b):** chỉ cấp khi tài khoản đã tạo ít nhất 30 ngày **và** ngày sinh được khai trước sinh nhật ít nhất 30 ngày. Khóa "không đổi trong 60 ngày quanh ngày sinh" áp cho cả lần khai đầu tiên. Khách thật không thiệt (năm sau vẫn nhận), kẻ lợi dụng phải chờ ít nhất 30 ngày cho mỗi tài khoản | Trả lời "Q3 đồng ý" hoặc nêu số ngày khác. Có thể cần ghi chú trên trang đăng ký: "Voucher sinh nhật áp dụng cho tài khoản từ 30 ngày" |
| Q4 | **Newsletter báo "email đã đăng ký"** (`BR-CNT-20`): người lạ dò được email nào đã đăng ký bản tin của quán | (a) Giữ đúng SRS. (b) Luôn trả cùng một câu "Vui lòng kiểm tra hộp thư" | **(b).** Không lộ thông tin ai là khách của quán. Người đã đăng ký vẫn nhận thư báo "bạn đã đăng ký rồi" (có giới hạn tần suất), người mới nhận thư xác nhận như cũ | Trả lời "Q4 đồng ý". Việc này làm lệch câu chữ của `BR-CNT-20`; người làm sẽ ghi vào mục tài liệu cần sửa |
| Q5 | **Khi Cloudflare Turnstile lỗi** (`SEC-57`): hiện từ chối hẳn ⇒ Cloudflare lỗi thì khách không đặt bàn/đặt món được | (a) Giữ nguyên (chặt nhất, có thể mất đơn). (b) Cho qua nhưng siết giới hạn tần suất như cột bên | **(b):** khi không gọi được Cloudflare thì cho qua, nhưng giới hạn theo IP giảm còn 1/3, ghi log cảnh báo và báo SuperAdmin nếu kéo dài quá 10 phút. Đúng gợi ý của SRS: "đặt bàn/đặt món không được ngừng" (`PERF-43`) | Trả lời "Q5 chọn (b)" hoặc giữ (a) |
| Q6 | **Pháp lý dữ liệu cá nhân** (`D-30`, Nghị định 13/2023 và luật bảo vệ dữ liệu cá nhân mới) | Không có lựa chọn kỹ thuật: cần ý kiến người có chuyên môn | Hệ thống đã sẵn: lưu đồng ý (`ConsentRecords`), xuất dữ liệu, xóa tài khoản sau 7 ngày, che SĐT/email, audit khi xem | **Bắt buộc trước khi mở web.** Hỏi luật sư hoặc dịch vụ tư vấn: có phải lập hồ sơ đánh giá tác động không, thời gian lưu dữ liệu, nội dung điều khoản. Gửi lại kết luận để người làm chỉnh thời hạn lưu (`DB-13`) và trang chính sách |
| Q7 | **Nội dung trang pháp lý** (`D-37`, `FR-CNT-04`): điều khoản, chính sách bảo mật, chính sách hủy | Không có lựa chọn kỹ thuật | Bản nháp đã ở `docs/legal/`; trang chưa có nội dung trả 404 | Điền chỗ trống (tên pháp nhân, mã số thuế, địa chỉ, email liên hệ), nhờ người ở Q6 duyệt, rồi đưa người làm nhập vào trang tĩnh |
| Q8 | **Thang điểm và hạng** (`D-34`) | Các con số đang là mặc định tạm: 1 điểm/10.000 ₫, hạng 300/1.000/3.000 điểm, giảm 3/5/8% | Giữ mặc định khi mở web, xem lại sau 2–3 tháng theo báo cáo khách hàng | Không cần trả lời ngay. Mọi con số sửa được ở trang Cài đặt, không cần sửa mã |
| Q9 | **Nơi chạy hệ thống khi triển khai** | (a) Một máy chủ ảo (VPS) chạy Docker: API, SQL Server, Redis/Valkey, Nginx. (b) Dịch vụ đám mây có quản lý (đắt hơn, ít việc vận hành hơn) | **(a)** với một quán một chi nhánh: VPS 4 vCPU / 8 GB RAM là đủ; Redis dùng **Valkey** (giấy phép BSD) chạy trong mạng nội bộ Docker, không mở cổng ra ngoài; sao lưu SQL mỗi đêm ra nơi lưu trữ khác máy chủ | Chọn nhà cung cấp VPS, mua tên miền, tạo tài khoản gửi email (`D-33`, đề xuất Brevo) và khóa Turnstile. Người làm sẽ ghi sẵn danh sách khóa cấu hình cần điền ở phần triển khai |
| Q10 | **Giờ mở cửa và ca phục vụ thật** | Không có lựa chọn kỹ thuật | CSDL mới mặc định "đóng cửa" cả 7 ngày; CSDL dev đang có giờ mẫu | Gửi giờ mở cửa từng ngày, các ca (giờ bắt đầu/kết thúc, số bàn tối đa mỗi ca) hoặc tự nhập ở trang quản trị khi có giao diện |
| Q11 | **Pháp lý mini game** (`D-23`) | Không có lựa chọn kỹ thuật | Phase 1 không có game | Chỉ cần trước Phase 2: hỏi chuyên môn về quy định khuyến mại và trò chơi có thưởng |
| Q12 | **Người dịch en/ja** | Tự dịch, thuê người dịch, hoặc dùng máy dịch rồi người duyệt | Máy dịch tạo bản nháp, **người biết tiếng duyệt trước khi bật ngôn ngữ**; trang ngôn ngữ chưa duyệt đủ thì chưa bật (`i18n.enabledLocales`) | Cho biết ai duyệt bản tiếng Anh và tiếng Nhật |

---

## 2. Thứ tự ưu tiên đề xuất

**A. Bắt buộc trước khi mở web cho khách (an toàn và không mất dữ liệu)**

1. Giữ bền khóa Data Protection (`SEC-16`): mất khóa là mất toàn bộ 2FA của nhân viên và liên kết hủy bản tin.
2. Sao lưu CSDL mỗi đêm ra nơi khác và diễn tập khôi phục (`SEC-72`).
3. Khóa đăng nhập đúng chuẩn (`BR-AUTH-03`, `SEC-51`), thu hồi access token khi đăng xuất (`SEC-50`), cảnh báo đăng nhập quản trị từ thiết bị lạ (`SEC-58`).
4. Log có cấu trúc, che dữ liệu nhạy cảm (`SEC-53`) và `/health/ready`.
5. Dọn dữ liệu theo vòng đời (`DB-13`), cần kết luận của Q6.
6. Việc định kỳ chạy theo giờ VN cố định: sinh nhật, hết hạn điểm.
7. Chốt Q1 (thư bị bỏ), Q3 (voucher sinh nhật), Q6, Q7.

**B. Làm cùng giao diện** (cần màn hình mới dùng được): sinh client TypeScript từ OpenAPI; trang hàng thư thất bại và hạn mức; cảnh báo nợ điểm; nhắc `Pending` quá hạn; STK + QR; giỏ hàng; CSP cho video nhúng.

**C. Làm ở phần triển khai:** Redis/Valkey (rate limit, cache, backplane SignalR, vé hub), reverse proxy `/api`, giới hạn tốc độ tải (`SEC-76`), `security.txt`.

**D. Để sau hoặc tùy chọn:** AVIF, làm nét ảnh, MessagePack, ghép bàn, app native, game (Phase 2).

---

## 3. Xác thực & tài khoản

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Ảnh đại diện khách | `FR-AUTH-08` | Đã có hồ sơ; còn cột `AvatarImageId` và `POST /account/avatar` | Dùng lại pipeline ảnh NetVips: chỉ nhận JPEG/PNG/WebP ≤ 5 MB, cắt vuông 256/512 px, mã hóa lại (tự bỏ EXIF có vị trí GPS), tối đa 5 lần đổi/ngày. Ảnh cũ vào danh sách dọn ảnh mồ côi |
| Chặn email dùng một lần khi nhận ưu đãi | `BR-AUTH-16` | Chưa có | Nhúng danh sách tên miền email tạm (bản công khai, giấy phép CC0), cập nhật theo bản phát hành. Kiểm lúc đăng ký và lúc nhận ưu đãi (voucher chào mừng, sinh nhật); chỉ chặn nhận ưu đãi, không chặn đăng ký để không mất khách thật |
| Gắn đơn/đặt bàn vãng lai vào tài khoản | `BR-AUTH-17`, khóa `loyalty.claimGuestOrders` | Khóa có trong cài đặt nhưng chưa có tác dụng | Chỉ gắn khi email tài khoản **đã xác thực** trùng email của đơn **và** SĐT trùng; khách bấm "Nhận đơn cũ" chứ không tự động. Chỉ đơn trong 30 ngày gần nhất, cộng điểm hồi tố một lần, có audit. Tránh việc ai đó đăng ký bằng email người khác để lấy điểm |
| Client native: refresh token trong body | `BR-AUTH-12` | Phase 3 | Chưa làm. Khi làm: chỉ nhận khi có header `X-Client: native` kèm khóa thiết bị; web vẫn chỉ dùng cookie `HttpOnly` |
| Thu hồi access token khi đăng xuất | `SEC-50` | Access token còn dùng được tối đa 15 phút sau đăng xuất | Lưu `jti` của token đã đăng xuất vào danh sách chặn tới khi token hết hạn: bộ nhớ đệm bây giờ, Redis khi chạy nhiều máy. Mỗi request chỉ tra thêm một khóa, gần như không tốn chi phí |
| Khóa đăng nhập đúng chuẩn | `BR-AUTH-03`, `SEC-51` | Bộ đếm chỉ về 0 khi đăng nhập đúng hoặc khi bị khóa | Đếm theo cửa sổ trượt 15 phút, tách hai bộ đếm (theo tài khoản và theo tài khoản + IP). Từ lần sai thứ 3 bắt Turnstile; thứ 5 khóa 15 phút và gửi email cảnh báo cho chủ tài khoản. Bộ đếm theo IP chặn dò nhiều tài khoản. Khóa theo tài khoản + IP để kẻ xấu không khóa được khách thật từ IP khác |
| Danh sách mật khẩu phổ biến lớn hơn | `BR-AUTH-01` | Khoảng 80 mục | Thay bằng danh sách 10.000 mật khẩu phổ biến nhất (bản công khai, giấy phép MIT), thêm các mẫu tiếng Việt ("matkhau", "123456a@", tên quán). Không gọi dịch vụ ngoài để không gửi mật khẩu đi đâu |
| Cảnh báo đăng nhập quản trị từ thiết bị/IP mới | `SEC-58`, email #23/#29 | Chưa có | Lưu dấu thiết bị (băm user-agent + dải IP /24) khi đăng nhập thành công. Gặp dấu mới thì gửi email cho chính nhân viên đó, kèm nút "Không phải tôi" để thu hồi mọi phiên. Đăng nhập SuperAdmin từ thiết bị mới thì báo thêm các SuperAdmin khác |

## 4. Nhân viên

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Step-up cho điều chỉnh điểm lớn | `SEC-61` | Đã áp cho nhân viên, xuất dữ liệu, `payment.*` | Thêm khóa cài đặt `loyalty.adjustStepUpThreshold` (mặc định 500 điểm): điều chỉnh từ ngưỡng trở lên phải nhập lại mật khẩu/mã 2FA, bên cạnh duyệt bốn mắt đã có |
| Bật 2FA tự nguyện; tạo lại mã khôi phục | `2fa/recovery` | Chưa có | Cho mọi vai trò tự bật 2FA ở trang tài khoản. Tạo lại mã khôi phục cần step-up, mã cũ hết hiệu lực ngay, gửi email báo |
| "Màn hình ca" bằng PIN cho bếp/thu ngân | `SEC-74` | Chưa có | Làm cùng giao diện quản trị: máy bếp/quầy đăng ký thiết bị một lần bằng tài khoản Manager; nhân viên trong ca chỉ nhập PIN 6 số để ghi ai bấm gì. PIN sai 5 lần thì khóa PIN; PIN chỉ dùng được trên thiết bị đã đăng ký |

## 5. Bảo mật & vận hành

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Bộ đếm rate limit, `IActionThrottle`, cache security stamp, cache giờ mở cửa | `09` §6, `PERF-11` | Trong bộ nhớ từng tiến trình | Chuyển sang Redis/Valkey ở phần triển khai. Redis lỗi thì quay về bộ đếm cục bộ chặt hơn (`PERF-43`). Chạy một máy thì bản hiện tại đã đúng |
| Backplane Redis cho SignalR; vé kết nối hub và bộ đếm kết nối | `06` §B.2, `SEC-69` | Trong bộ nhớ tiến trình | Làm cùng mục trên. Vé lưu Redis có TTL 30 giây, lấy và xóa vé bằng một lệnh nguyên tử (`GETDEL`) |
| Realtime: MessagePack, đo số kết nối và độ trễ | `PERF-63` | Chưa có | Chỉ cần đo (số kết nối, thời gian từ commit tới gửi) qua OpenTelemetry. MessagePack không đáng làm vì payload rất nhỏ |
| Log có cấu trúc, che dữ liệu nhạy cảm, chống log injection | `SEC-53` | Log mặc định của .NET | Serilog ghi JSON có `traceId`; bộ lọc che SĐT/email/token/mật khẩu theo tên trường và theo mẫu; bỏ ký tự xuống dòng trong giá trị người dùng nhập; xóa query `ticket`, `token`, `code` khỏi đường dẫn trong log (`SEC-46`) |
| `/health/ready` | `02` | Chỉ có `/health/live` | Kiểm SQL Server, Redis (khi có), hộp thư đi không dồn quá 15 phút. `/health/ready` chỉ mở trong mạng nội bộ, không công khai |
| OpenAPI 3.1 + client TypeScript sinh tự động | `FR-ARC-03`, `09` §1 | Đã có OpenAPI + Scalar ở Development | Làm **đầu tiên** khi bắt đầu giao diện: sinh client TypeScript trong CI để frontend không gõ tay đường dẫn/kiểu dữ liệu; CI báo lỗi khi API đổi mà client chưa sinh lại |
| `security.txt`, runbook sự cố | `SEC-60` | Chưa có | Tệp `security.txt` với email báo lỗ hổng. Runbook một trang: lộ mật khẩu nhân viên, nghi rò dữ liệu, mất máy chủ, Cloudflare/email lỗi; mỗi tình huống ghi ai làm gì trong 1 giờ đầu |
| Dọn dữ liệu theo vòng đời | `DB-13` | Chưa có | Một việc định kỳ chạy 03:00 giờ VN, xóa theo lô nhỏ (1.000 dòng/lần) để không khóa bảng: outbox đã xử lý > 7 ngày, OTP > 1 ngày, `EmailLogs` > 90 ngày, tài khoản chưa xác thực > 30 ngày, refresh token hết hạn > 30 ngày. Thời hạn chốt theo kết luận Q6 |
| Việc định kỳ chạy theo giờ VN cố định | `20` §4 | Bộ chạy chỉ theo chu kỳ | Thêm kiểu lịch "chạy lúc HH:mm giờ VN mỗi ngày", lưu lần chạy cuối trong CSDL để không chạy hai lần khi khởi động lại và chạy bù nếu máy tắt đúng giờ đó. Dùng cho sinh nhật (08:00), hết hạn điểm (01:00), dọn dữ liệu (03:00) |
| Turnstile khi Cloudflare lỗi | `SEC-57` | Từ chối hẳn | Theo Q5 |
| Tách migration bật RCSI | `DB-09` | Đang chung migration | Không làm: đã chạy đúng, tách ra chỉ thêm rủi ro |
| Lưu khóa Data Protection bền + mã hóa | `SEC-16` | Lưu thư mục khi có cấu hình `DataProtection:KeysPath` | Bắt buộc khi triển khai: thư mục nằm trên ổ bền ngoài container, mã hóa khóa bằng chứng chỉ X.509, có trong bản sao lưu. Thêm kiểm tra khi khởi động: môi trường Production thiếu cấu hình thì dừng |
| Hai SuperAdmin cùng sửa giờ mở cửa thường kỳ đúng cùng lúc | `BR-TBL-16` | Có thể cùng qua kiểm ETag | Đọc với khóa cập nhật (`UPDLOCK`) trong cùng transaction với lúc ghi. Rủi ro rất nhỏ, làm khi tiện |
| Sao lưu và khôi phục | `SEC-72` | Chưa có | Sao lưu đầy đủ mỗi đêm + log giao dịch mỗi 15 phút, mã hóa, đẩy ra nơi lưu trữ khác máy chủ, giữ 30 ngày. Mỗi quý khôi phục thử lên máy sạch |

## 6. Email & thông báo

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Thư vượt giới hạn bị bỏ hẳn (thư xác nhận của khách, thư gộp "cần xử lý") | `BR-NTF-08`, `docs/srs/20` #30 | Danh sách `GET /admin/orders/attention` không bị ảnh hưởng | Theo Q1 |
| Trang quản trị: hàng thư thất bại, thống kê hạn mức, nút "Gửi email thử" | `BR-NTF-01`, `BR-NTF-07`, `BR-NTF-12` | API gửi thử đã có | Làm cùng giao diện quản trị: danh sách thư thất bại có nút gửi lại; biểu đồ số thư/ngày so với hạn mức |
| Cảnh báo Manager khi dùng ≥ 80% hạn mức email | `BR-NTF-12` | Chưa có | Việc định kỳ mỗi giờ: vượt 80% thì gửi một thư (mỗi ngày tối đa một lần) và hiện cảnh báo trên bảng tin |
| Xử lý bounce/complaint | `BR-NTF-11`, `BR-CNT-26` | Chưa có | Nhận webhook của nhà cung cấp (Brevo), kiểm chữ ký webhook. Bounce cứng hoặc báo spam ⇒ đánh dấu địa chỉ không gửi được, chỉ còn thư giao dịch bắt buộc. Theo dõi tỷ lệ báo spam, vượt 0,1% thì tạm dừng bản tin |
| Thư nội bộ: đặt bàn mới (#3), khách hủy (#7), cảnh báo quản trị (#29), tóm tắt (#30) | `20` §2, `BR-NTF-09` | Đã có realtime cho lễ tân/quản lý | Hạn mức thư miễn phí chỉ vài trăm thư/ngày (`D-33`) nên **không gửi thư cho từng sự kiện**: nhân viên đang trực xem qua realtime; thư chỉ gửi dạng tóm tắt mỗi 30 phút khi có việc chưa xử lý. Cảnh báo bảo mật (#29) vẫn gửi ngay |
| Các thư còn lại theo danh mục | `20` §2 | Đã làm thư của các phần đã xong | Rà lại danh mục `20` §2 khi kiểm thử cuối, đánh dấu từng mục đã có hoặc không cần |

## 7. Bàn & giờ mở cửa

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Ghép bàn | `BR-TBL-09` | Phase 2 | Chưa làm |
| Nhân viên tự đổi trạng thái bàn chưa phát sự kiện realtime | `RT-05` | Đổi qua đặt bàn đã có `reservation.statusChanged` | Thêm sự kiện `table.statusChanged` (chỉ `id`, `status`) cho nhóm có quyền xem bàn. Ghi bổ sung vào danh sách sự kiện của `06` §B.2 |
| Tự đặt bàn về "Đã đặt" đầu ngày cho đặt bàn đã xác nhận hôm nay | `BR-RSV-09` | Chỉ đồng bộ khi đổi trạng thái/gán bàn | Việc định kỳ 00:05 giờ VN (dùng lịch theo giờ cố định ở mục 5), không ghi đè bàn đang "Dọn dẹp" hoặc "Có khách" |

## 8. Đặt bàn

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Nhắc nhân viên khi `Pending` quá hạn, báo Manager | `BR-RSV-25`, khóa `reservation.pendingAlertMinutes` | Tự hủy khi tới giờ cắt đã có | Việc định kỳ 5 phút, chỉ chạy trong giờ mở cửa: đếm đặt bàn chờ quá `pendingAlertMinutes`, hiện số trên bảng tin lễ tân và gửi thư tóm tắt cho Manager (theo mục 6) |
| Hạn hủy đặt bàn | `BR-RSV-06` | Khóa `reservation.cancellationHours` đã có tác dụng, mặc định 0 (hủy tự do) | Không cần làm thêm. Quán muốn siết thì đổi ở Cài đặt |

## 9. Thực đơn

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Nhập/xuất Excel: nhóm tùy chọn, mô tả đầy đủ, nhập danh mục | `BR-MNU-14` | Bản hiện tại chỉ món | Thêm sheet riêng cho danh mục và nhóm tùy chọn, giữ quy trình xem trước rồi mới ghi |
| Chống zip bomb khai sai kích thước | `SEC-11`, `BR-MNU-14` | Kiểm kích thước khai trong zip + tệp ≤ 2 MB + chỉ Manager/SuperAdmin | Đọc từng phần của tệp qua luồng có đếm byte thật, vượt 30 MB thì dừng ngay. Làm sớm vì chi phí nhỏ |
| Ảnh món, ảnh danh mục | `BR-MNU-06`, `BR-MNU-12` | Pipeline ảnh đã có | Thêm cột và DTO, làm trước giao diện khách vì trang thực đơn cần ảnh |
| `GET /menu/items/{slug}/reviews` | `docs/srs/15` §4 | Đánh giá đã có | Làm cùng giao diện khách; có cache ngắn và phân trang |
| Xóa tùy chọn đã có trong đơn ⇒ chỉ được ẩn | `BR-MNU-23` | Vẫn xóa thẳng (đơn cũ giữ bản chụp) | Tùy chọn đã xuất hiện trong đơn thì trả 409 kèm gợi ý "ẩn"; chưa từng dùng thì xóa được. Giữ được thống kê món |
| Cache thực đơn nhiều máy | `PERF-10` | `IMemoryCache` một tiến trình | HybridCache + Redis ở phần triển khai |
| Test đếm truy vấn `GET /menu/items` ≤ 2 | `PERF-51`, `TST-28` | Chưa có | Thêm bộ đếm truy vấn EF trong test tích hợp ở phần kiểm thử cuối |

## 10. Voucher & khuyến mãi

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Lợi dụng voucher sinh nhật bằng tài khoản mới | `BR-LOY-20` | Chỉ yêu cầu email đã xác thực | Theo Q3 |
| Banner khuyến mãi | `docs/srs/08` §6 | Pipeline ảnh đã có | Thêm cột và DTO cùng lúc với ảnh món |
| Tắt tự động khuyến mãi hết hạn | `docs/srs/14` §3 | Hiệu lực tính khi truy vấn | Không cần làm |
| Voucher giới hạn theo SĐT/email cho khách vãng lai | `D-16` | Đã chốt bắt buộc đăng nhập | Không làm |

## 11. Điểm & hạng

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Đối soát hằng đêm điểm hiện có = tổng sổ cái = tổng lô | `BR-LOY-04` | Bất biến đã giữ trong mọi thao tác | Việc định kỳ 02:00 giờ VN, chỉ đọc. Lệch thì **không tự sửa**, chỉ báo SuperAdmin kèm danh sách tài khoản lệch (điểm là tài sản của khách, sửa phải có người duyệt) |
| Cảnh báo nhân viên khi khách có nợ điểm | `BR-LOY-08` | Số nợ đã có ở API | Hiện cảnh báo ở màn hình khách và màn hình thu ngân |
| Điểm/vé game dùng chung sổ cái | `BR-LOY-13` | Phase 2 | Chưa làm |

## 12. Đặt món mang về

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Nhắc "cần xử lý" cho thu ngân trên màn hình | `BR-ORD-18`, `docs/srs/20` #30 | Có thư gộp cho quản lý mỗi 10 phút và `GET /admin/orders/attention` | Giao diện quản trị hiện huy hiệu số việc cần xử lý, gọi lại danh sách khi nhận sự kiện `order.*` và mỗi 60 giây (phòng khi mất sự kiện) |
| STK + QR trên trang đặt món | `FR-ORD-03` | API đã có | Làm ở giao diện khách; nội dung chuyển khoản tự điền mã đơn để thu ngân dễ đối chiếu |
| Turnstile chỉ bắt buộc với khách vãng lai | `BR-ORD-20` | Áp cho mọi lượt đặt khi bật | Bỏ Turnstile khi có access token khách hợp lệ (đã có giới hạn theo tài khoản). Làm nhanh, giúp khách quen đặt nhanh hơn |
| Gộp giỏ khi đăng nhập, kiểm lại giá khi mở giỏ | `FR-ORD-01` | `POST /orders/quote` đã có | Làm ở giao diện khách; giá luôn lấy từ server, giỏ trong trình duyệt chỉ lưu id món và số lượng |

## 13. Nội dung (CMS) và ảnh

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Slug cũ 301 cho món và khuyến mãi | `BR-CNT-41` | Bảng `SlugRedirects` mới dùng cho tin tức | Dùng chung bảng, làm cùng giao diện khách (SSR) |
| `Location` 301 từ đường dẫn API sang đường dẫn trang | `BR-CNT-41` | Đang trỏ đường dẫn API | Làm ở giao diện khách |
| Đưa nội dung pháp lý vào trang tĩnh | `FR-CNT-04` | Trang chưa có nội dung trả 404 | Theo Q7 |
| Header one-click hủy bản tin cần reverse proxy chuyển `/api` về API | `BR-CNT-26` | Chưa có | Cấu hình Nginx ở phần triển khai |
| Giữ bền khóa Data Protection (liên kết hủy bản tin) | `BR-CNT-24` | Gộp với `SEC-16` | Như mục 5 |
| Newsletter báo "email đã đăng ký" | `BR-CNT-20` | Làm đúng SRS | Theo Q4 |
| Thư báo liên hệ nội bộ chịu giới hạn 3 thư/giờ | `BR-CNT-33`, `BR-NTF-08` | Tin vẫn nằm trong danh sách chưa đọc; quản lý đã nhận realtime | Gộp theo Q1 |
| Cache danh sách đánh giá công khai | `PERF-11/12` | Đọc thẳng DB, có phân trang và chỉ mục | Cache 60 giây theo trang, xóa khi duyệt đánh giá mới |
| Dọn ảnh mồ côi sau 30 ngày | `BR-CNT-32`, `FR-IMG-10` | Xóa banner/gallery thì ảnh ở lại | Việc định kỳ hằng đêm: ảnh không còn được tham chiếu quá 30 ngày thì xóa gốc + biến thể; ghi log số tệp đã xóa |
| Sinh lại biến thể khi đổi cấu hình, AVIF | `FR-IMG-03/04` | `Version` đã có | Lệnh SuperAdmin chạy nền, xử lý theo lô. AVIF để sau (WebP đã đủ cho mọi trình duyệt phổ biến) |
| Ảnh xử lý nền bị `Failed` | `FR-IMG-08` | Ảnh gốc vẫn còn | Tự thử lại 3 lần cách nhau 5 phút, vẫn lỗi thì hiện cảnh báo ở thư viện ảnh và có nút "Thử lại" |
| Làm nét nhẹ khi thu nhỏ ảnh món | `FR-IMG-07` | Chưa có | Tùy chọn, làm khi chỉnh giao diện |
| Giới hạn tốc độ tải dữ liệu chậm | `SEC-76` | Chưa có | Cấu hình ở Nginx (`client_body_timeout`, giới hạn kết nối) ở phần triển khai |
| CSP `frame-src` cho YouTube/TikTok | `BR-CNT-35`, `SEC-09` | Chưa có | Chỉ cho đúng hai tên miền nhúng; làm ở giao diện khách |
| Kiểm giấy phép `SixLabors.Fonts` (gói gián tiếp của ClosedXML) | `D-04` | Chưa kiểm | Người làm kiểm điều khoản. Nếu vướng thì tắt tính năng tự co cột của ClosedXML (dùng độ rộng cố định) để bỏ gói này |

## 14. Báo cáo & cài đặt

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Xuất báo cáo PDF | `BR-RPT-13`, `D-04` | `format=pdf` trả 400 | Theo Q2 |
| Dữ liệu cũ chưa có trong `DailyStats` | `PERF-55` | Job chỉ tính 2 ngày gần nhất, đối soát 7 ngày | Thêm lệnh SuperAdmin "tính lại khoảng ngày" (tối đa 366 ngày một lần), chạy nền từng ngày một, có audit. Chỉ cần khi nhập dữ liệu cũ |
| Khóa cài đặt chưa có tác dụng: `reservation.pendingAlertMinutes`, `loyalty.claimGuestOrders`, `otp.channel` khác email | `19` §3.1 | Sửa các khóa này chưa đổi hành vi (`i18n.*` đã có tác dụng) | Mỗi khóa có tác dụng khi làm tính năng tương ứng (mục 3, 8). Trong lúc chờ, trang cài đặt ghi "chưa có hiệu lực" cạnh các khóa này. `otp.channel` chỉ nhận `email` tới khi có nhà cung cấp SMS |

## 15. Đa ngôn ngữ

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Phần giao diện: tệp XLF vi/en/ja và CI chặn khi thiếu khóa, URL `/en/…` `/ja/…`, `hreflang`, sitemap theo ngôn ngữ, nhãn "Chỉ có tiếng Việt" khi `isFallback`, dải gợi ý theo `Accept-Language`, cookie `ot-lang`, định dạng ngày/tiền theo ngôn ngữ, ghi chú "bản dịch tham khảo" ở trang pháp lý | `BR-I18N-07..12`, `22` §4.1 | API đã trả `contentLang`, `isFallback` | Làm ở giao diện khách. Thông điệp lỗi lấy theo `errorCode` trong XLF, không hiện chuỗi từ server |
| Bảng thuật ngữ `docs/i18n/glossary.md` và CI kiểm thuật ngữ bắt buộc trong tệp XLF | `BR-I18N-05` | SRS có bảng khởi đầu `22` §5 | Tạo tệp từ bảng khởi đầu khi làm giao diện; người duyệt (Q12) xác nhận, nhất là bản tiếng Nhật |
| Tên danh mục tin tức chưa có bản dịch | `22` §2 | Hiện tiếng Việt ở mọi ngôn ngữ | SRS không liệt kê bảng dịch cho danh mục tin; tin tức là "tùy chọn" nên giữ nguyên. Nếu cần thì thêm `NewsCategoryTranslations` theo cùng mẫu |
| Tìm tin tức theo tiêu đề bản dịch | `22` §4.2 | Tìm theo tiêu đề tiếng Việt không dấu | Thực đơn đã tìm được cả tên bản dịch. Tin tức để sau vì ít bài |
| Lỗi `LOCALE_NOT_READY` khi bật ngôn ngữ chỉ liệt kê tối đa 200 mục còn thiếu | `BR-I18N-06` | Danh sách đầy đủ ở `GET /admin/translations/progress?lang=` | Giao diện hiện danh sách từ màn hình tiến độ dịch |

## 16. Realtime (SignalR)

| Việc | Mã | Hiện trạng | Đề xuất |
|---|---|---|---|
| Bảng xếp hạng trực tiếp, vé/phần thưởng theo người dùng | `RT-07`, `RT-08` | Phase 2 (game) | Chưa làm. Nhóm `user:{id}` dùng lại vé kết nối khách đã có |
| Sự kiện cho bàn | `RT-05` | Xem mục 7 | |

## 17. Tài liệu SRS cần cập nhật theo cách đã làm

Người làm tự sửa được, không cần quyết định. Gom lại để sửa một lần trước kiểm thử cuối.

| Việc | Mã |
|---|---|
| `docs/srs/18`: `STEP_UP_REQUIRED` trả 401 theo nghiệm thu `19` §5; frontend phân biệt với hết phiên bằng `errorCode` | `SEC-61` |
| `docs/srs/06`, `09`: đường xin vé là `POST /api/v1/admin/realtime/ticket` và `POST /api/v1/account/realtime-ticket`, không phải `POST /hubs/ticket` | `RT-09` |
| `docs/srs/06`: vé chỉ dùng được với WebSocket; hub nhân viên chỉ nhận WebSocket, hub công khai ẩn danh nhận mọi transport | `RT-09`, `06` §B.2 |
| `docs/srs/06`: `order.statusChanged` gửi cả khi đổi thanh toán/xác nhận liên hệ, payload có thêm `paymentStatus` | `06` §B.2 |
| `docs/srs/08`: bảng `DataExports`, cột `Users.DeletionScheduledAtUtc`; `Orders.CustomerPhone` để trống sau ẩn danh hóa | `docs/srs/08` |
| `docs/srs/08`: `NewsletterSubscribers` thêm `ConfirmTokenHash`, `ConfirmExpiryUtc`, `UnsubscribeTokenProtected`; `NewsletterRecipients` thêm `Attempts`; `ConsentRecords` thêm `Email`, `UserId` cho phép trống | `docs/srs/08` |
| `docs/srs/08`: các cột đơn hàng/voucher phát sinh (xem `TIEN_DO.md` mục 6) | `docs/srs/08` |
| `docs/srs/22` §4.2: API chọn ngôn ngữ chỉ bằng `?lang=`, không đọc `Accept-Language` — trình duyệt luôn gửi header này nên đọc nó sẽ tự chuyển ngôn ngữ, trái `BR-I18N-08`, và làm cache phải tách theo header | `22` §4.2 |
| `docs/srs/22` §4.3: đường bản dịch nội dung là `PUT /admin/translations/{promotion|banner|gallery|news|static_page}/{id hoặc slug}/{en|ja}`; tiến độ dịch `GET /admin/translations/progress?lang=` | `22` §4.3 |
| `docs/srs/08`: bảng `GalleryTranslations` dùng cột `Caption`; các bảng bản dịch nội dung chỉ có cột mà bản Việt có | `08` §2 |
| Ghi lại quyết định Q1, Q3, Q4, Q5 sau khi chủ dự án chốt | `91` |

## 18. Chưa bắt đầu (theo thứ tự dự kiến)

Làm hết backend rồi mới làm giao diện (chủ dự án đã chốt).

Giao diện khách → Giao diện quản trị → PWA → Triển khai (Docker, Nginx, Redis, sao lưu, Cloudflare) → Kiểm thử cuối (E2E, hiệu năng, bảo mật).
