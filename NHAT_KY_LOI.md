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

## Đặt món mang về – bước 1 (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Lệnh shell dài (heredoc chứa script Python tạo nhiều file C#) báo `unexpected EOF while looking for matching` và không chạy gì — lặp lại lần thứ ba | Lệnh quá dài/nhiều dấu nháy làm bộ phân tích lệnh của công cụ chạy shell hỏng, dù heredoc đã đặt `'EOF'` | Ghi script Python ra thư mục tạm bằng công cụ ghi file rồi chạy `python <file>` | Mọi đoạn tạo nhiều file: viết script thành file trước, không nhét vào heredoc |
| Helper test báo `The value of 'OptionTranslation.EntityId' is unknown when attempting to save changes` | Thêm bản dịch dùng `large.Id` khi tùy chọn chưa được lưu (Id còn 0, EF coi khóa ngoại chưa xác định) | Gọi `SaveChangesAsync` sau khi thêm tùy chọn, rồi mới thêm liên kết/bản dịch | Thực thể có Id do CSDL sinh: lưu xong mới dùng Id |
| Test hiện liên hệ mong `0949…` nhưng nhận `+84949…` | SĐT lưu dạng chuẩn hóa E.164 (giống đặt bàn) — đúng thiết kế | Sửa kỳ vọng của test | Nhớ quy ước SĐT chuẩn hóa khi viết test |

## Thực đơn – bước 2: nhập/xuất Excel (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Nhập Excel báo `committed: true` nhưng giá món không đổi trong CSDL | `EfUnitOfWork.InTransactionAsync` gọi `ChangeTracker.Clear()` đầu mỗi lần thử (để chạy lại được khi lỗi tạm thời); món nạp lúc lập kế hoạch bị tách khỏi context nên sửa vào không được lưu | Nạp lại món **bên trong** transaction (`ApplyAsync` gọi `FindItemsBySlugAsync`); món biến mất giữa lúc xem trước và lúc ghi ⇒ 412 | Mọi thực thể sửa trong `InTransactionAsync`/`TryInTransactionAsync` phải nạp trong chính hàm `work`; đã rà các dịch vụ khác, không còn chỗ nào |
| Tệp quá lớn gửi qua Kestrel thật trả `400 VALIDATION_FAILED` (`errors: {"": ["INVALID_FORMAT"]}`) thay vì 413 | Action có tham số ⇒ `FormValueProviderFactory` của MVC đọc form ngay lúc binding, bắt `BadHttpRequestException` và đổi thành lỗi ModelState | Action nhập không có tham số: tự đọc `commit` từ query, kiểm `Content-Length` rồi `Request.ReadFormAsync` | Endpoint tải tệp có giới hạn dung lượng: không bind `IFormFile`/tham số; test bằng Kestrel thật (`ApiFactory.UseRealServer()`), TestServer không áp giới hạn body |
| Gửi thân không có `Content-Length` (chunked) vượt giới hạn ⇒ 500 `INTERNAL_ERROR` và log lỗi | Bộ xử lý ngoại lệ đổi mọi ngoại lệ thành 500, kể cả `BadHttpRequestException` (413/400) của Kestrel | `UseApiExceptionHandler`: `BadHttpRequestException` 413 ⇒ `FILE_TOO_LARGE`, loại khác ⇒ `VALIDATION_FAILED` | Lỗi do request luôn giữ mã 4xx |
| Test chạy trên Kestrel nhận trang HTML "Bad Request - Invalid Hostname" | `AllowedHosts` = `localhost`; gọi bằng `http://127.0.0.1:port` bị middleware lọc host chặn (trang lỗi giống HTTP.sys nên dễ tưởng nhầm) | Đổi host của địa chỉ server thành `localhost` | Gặp trang "Invalid Hostname" thì kiểm `AllowedHosts` trước |
| Client test nhận `connection forcibly closed` khi gửi tệp quá lớn lên Kestrel | Kestrel trả 413 rồi đóng kết nối trong lúc client còn gửi thân | Gửi kèm `Expect: 100-continue` (trình duyệt/Nginx xử lý tương tự) | — |
| Test mong ô xuất là `'=1+1` nhưng nhận `=1+1` | ClosedXML hiểu dấu `'` đầu chuỗi là cờ quotePrefix của Excel và bỏ dấu đó | Đặt rõ `Style.IncludeQuotePrefix = true` cho ô bắt đầu bằng `= + - @`; test kiểm cờ và kiểu ô là Text | Kiểm hành vi thư viện bằng test trước khi dựa vào |
| Test xuất Excel mất 8–15 giây | `AdjustToContents()` nạp font hệ thống để đo chữ | Đặt độ rộng cột cố định | Tránh API phụ thuộc font của máy chủ |
| `NameSearch nvarchar(300)` có thể tràn: tên Việt + en + ja mỗi tên ≤ 150 | Tính thiếu khi thiết kế cột | Migration `MenuNameSearchLength` nới lên 500, cắt an toàn ở `MenuItemMapping.SearchText` | Cột ghép từ nhiều trường: cộng độ dài tối đa từng phần |
| Sửa món (đổi giá) làm mất tên bản dịch khỏi `NameSearch` | Hàm gán dữ liệu cũ chỉ đặt `NameSearch` = tên Việt | Dùng chung `MenuItemMapping.SearchText` (tên Việt + bản dịch đã duyệt còn khớp) cho form, bản dịch và nhập Excel | Logic tính trường dẫn xuất chỉ để một chỗ |

## Thực đơn – bước 1: danh mục, món, tùy chọn, bản dịch (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Log API có `InvalidOperationException: StatusCode cannot be set because the response has already started` mỗi lần gọi endpoint quản trị không kèm token (khách vẫn nhận 401 đúng) | Endpoint chấp nhận cả hai scheme JWT nên cả hai cùng chạy `OnChallenge`; scheme đầu đã ghi ProblemDetails, scheme sau đặt lại `StatusCode` | Trong `OnChallenge`/`OnForbidden` của `AuthConfiguration`, nếu `Response.HasStarted` thì bỏ qua | Sau mỗi lần chạy thật, đọc log tìm `fail:`/`Exception` chứ không chỉ nhìn mã HTTP |
| `GET /menu/items?excludeAllergens=abc` có thể trả 500 | `MenuRules.ParseAllergens` dùng `First(...)` cho mọi mã, mã lạ ném `InvalidOperationException` | Đổi thành lọc các mã hợp lệ, bỏ qua mã lạ, không phân biệt hoa thường | Hàm đọc dữ liệu có thể đến từ tham số khách phải chịu được đầu vào rác; viết test cho giá trị lạ |
| Build lỗi `CS0104: 'IHtmlSanitizer' is an ambiguous reference between 'ObentoTensho.Core.Common.IHtmlSanitizer' and 'Ganss.Xss.IHtmlSanitizer'` | Gói HtmlSanitizer có interface cùng tên | Dùng bí danh `using IHtmlSanitizer = ObentoTensho.Core.Common.IHtmlSanitizer;` | Khi thêm gói ngoài, kiểm trùng tên với interface của dự án |
| Test sửa món sau khi duyệt bản dịch nhận `412` thay vì `200` | Duyệt bản dịch cập nhật `NameSearch` của món nên RowVersion (ETag) đổi — hành vi đúng | Test lấy ETag mới bằng `GET` trước khi `PUT` | Thao tác phụ nào ghi vào bảng chính cũng làm ETag cũ hết hiệu lực; giao diện quản trị phải tải lại món sau khi lưu bản dịch |

## Đặt bàn – lõi (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Build lỗi `CA1716` lần thứ tư (tham số `date`, `to`, namespace `Public`) | Luật dành cho thư viện dùng chung với VB/F#; dự án là ứng dụng nội bộ | Tắt `CA1716` ở `.editorconfig` gốc kèm lý do, bỏ `.editorconfig` riêng của Api | Khi một luật analyzer bị tắt cục bộ nhiều lần với cùng lý do, cân nhắc tắt toàn repo có ghi chú |
| Thiết kế: transaction qua `InTransactionAsync` luôn commit, kể cả khi nghiệp vụ trả thất bại (vd giữ chỗ được nhưng bước sau lỗi) | Hàm cũ không biết kết quả nghiệp vụ | Thêm `TryInTransactionAsync<T>` chỉ commit khi `Result.IsSuccess`, ngược lại rollback và xóa theo dõi | Thao tác nhiều bước có thể thất bại nghiệp vụ giữa chừng luôn dùng `TryInTransactionAsync` |
| Thiết kế: `TestSchedule` cũ xóa `ServiceSlots` sẽ lỗi khóa ngoại khi đã có đặt bàn | `Reservations.SlotId` không xóa dây chuyền (giữ lịch sử) | Helper `TestSchedule` xóa đặt bàn → sức chứa → ca → giờ; dịch vụ xóa ca trả `409 SLOT_HAS_RESERVATIONS` (chỉ được tắt ca) | Dữ liệu dùng chung giữa các lớp test đặt lại qua một helper duy nhất |

## Email và hộp thư đi (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Mọi test mẫu email đỏ `Thiếu mẫu email verify_email.vi`, dù `dotnet msbuild -getItem:EmbeddedResource` liệt kê đủ 12 tệp | Tệp tên `*.vi.txt`, `*.en.txt`, `*.ja.txt`: MSBuild coi `.vi/.en/.ja` là mã văn hóa nên tách sang assembly vệ tinh `bin/.../vi/*.resources.dll`, assembly chính không có | Thêm `WithCulture="false"` cho `EmbeddedResource` của mẫu; kiểm lại bằng `Assembly.GetManifestResourceNames()` (13 tài nguyên) | Tài nguyên nhúng có đuôi trùng mã ngôn ngữ luôn đặt `WithCulture="false"`; có test đếm số mẫu nạp được |
| `sed` sửa dòng có `Email\Templates\*.txt` trong `.csproj` không đổi gì (không báo lỗi) | Gạch chéo ngược và `*` trong mẫu sed bị shell/regex hiểu khác | Sửa bằng công cụ chỉnh sửa file rồi đọc lại dòng đó | Không dùng sed cho dòng có `\`; sau mỗi lần sửa bằng lệnh, `grep` lại kết quả |
| Lệnh python dài trong heredoc (mẫu email có tiếng Nhật, dấu gạch ngang dài) báo `unexpected EOF while looking for matching` | Như mục "Nền CSDL": heredoc dài bị shell phân tích sai | Ghi script vào thư mục tạm bằng công cụ ghi file rồi chạy `python script.py` | Nội dung dài/đa ngôn ngữ luôn ghi ra tệp trước |

## Bàn, khu vực, ca phục vụ, khóa ngày, giờ mở cửa (07/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Test audit đỏ: `NewDataJson` chứa `"B1 cửa sổ"` thay vì `"B1 cửa sổ"` | `System.Text.Json` mặc định mã hóa mọi ký tự ngoài ASCII; nhật ký vẫn đúng nhưng người xem đọc không được và tốn dung lượng | `AuditRedactor` ghi JSON với `JavaScriptEncoder.Create(UnicodeRanges.All)` (giữ chữ Việt/Nhật, vẫn mã hóa `<`, `>`, `&`) | JSON lưu CSDL để người đọc thì đặt Encoder rõ ràng; có test với chuỗi tiếng Việt |
| Test đỏ `ObjectDisposedException: Cannot access a closed Stream` ở helper đăng nhập test | Đọc body của cùng một `HttpResponseMessage` hai lần (`ReadFromJsonAsync` đóng luồng sau lần đầu) | Đọc `data` một lần rồi tách `challenge`/`session` từ cùng `JsonElement` | Mỗi phản hồi chỉ gọi đọc body một lần; tách dữ liệu sau |
| Build lỗi hàng loạt `CA1305`/`MA0011` ở `id.ToString()` và `CA1716` với tham số tên `to` | Analyzer bắt định dạng số theo văn hóa và tên trùng từ khóa VB | `ToString(CultureInfo.InvariantCulture)`; đổi tên tham số `fromDate`/`toDate` | Định danh trong log/audit luôn dùng InvariantCulture |

## Phân quyền, audit, giới hạn tần suất, header bảo mật (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Thêm `ProjectReference` tới `ObentoTensho.Api` vào `ArchTests` nhưng build lỗi `CS0234: The type or namespace name 'AspNetCore' does not exist` | Api dùng SDK Web (kéo theo framework ASP.NET Core), project test dùng SDK thường nên không thấy các kiểu ASP.NET Core qua tham chiếu project | Thêm `<FrameworkReference Include="Microsoft.AspNetCore.App" />` vào project test | Project thường muốn đọc kiểu của Api/Infrastructure phải khai báo cùng FrameworkReference |
| Build lỗi `CA1711: Rename type name StaffPermission so that it does not end in 'Permission'` | Hậu tố `Permission` dành cho kiểu quyền của .NET | Đổi tên enum thành `StaffCapability`, lớp ma trận thành `StaffCapabilities` | Tránh hậu tố `Permission`, `Attribute`, `Collection`… cho kiểu không thuộc loại đó |
| Test chức năng gọi đăng nhập nhiều lần từ cùng "IP" của TestServer có thể chạm giới hạn 10 lần/phút | Mọi yêu cầu của TestServer chung một địa chỉ (không có IP thật) | `ApiFactory` mặc định nâng giới hạn; test giới hạn tần suất tự hạ xuống 3; `TestRemoteIpStartupFilter` gán IP theo header `X-Test-Remote-Ip` để test proxy/IP thật | Giới hạn tần suất luôn đọc từ cấu hình, không hằng số trong mã |

## Xác thực – đăng nhập nhân viên, 2FA, lời mời (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| CI job "Quét bí mật" đỏ: gitleaks báo `generic-api-key` ở `TotpTests.cs` dòng 58 | Chuỗi Base32 của khóa mẫu công khai RFC 6238 đứng ngay sau `secret=` trong URI kỳ vọng nên khớp luật phát hiện API key (báo động giả) | Viết lại test để không ghép `secret=` với chuỗi cố định; thêm dấu vân tay `<commit>:<file>:<rule>:<line>` của phát hiện cũ vào `.gitleaksignore` kèm lý do (không viết lại lịch sử nhánh đã đẩy) | Dữ liệu mẫu trong test không đặt sau các từ khóa `secret=`, `key=`, `token=`; sinh từ mã (vd `Base32.Encode(...)`) thay vì chuỗi cố định |
| Test `Sai_TOTP_5_lan_thi_tai_khoan_bi_khoa` đỏ: sau 5 lần sai mã 2FA vẫn đăng nhập tiếp được (`200` thay vì `401`) — **lỗ hổng**: người biết mật khẩu đoán mã TOTP không giới hạn | Bước mật khẩu đúng gọi `ResetFailedLoginsAsync`, xóa luôn các lần sai 2FA trước đó; kẻ tấn công chỉ cần sai 4 lần rồi đăng nhập lại | Bỏ việc xóa bộ đếm ở bước mật khẩu; chỉ xóa khi đã qua 2FA hoặc khi cấp phiên (`StaffSignInFlow.StartSessionAsync`) | Với đăng nhập nhiều bước, trạng thái chống dò chỉ được đặt lại sau bước **cuối**; luôn có test "sai bước 2 nhiều lần" |

## Xác thực – đăng ký, đăng nhập, phiên của khách (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Tạo `appsettings.Development.json` cho Api nhưng `git status` không thấy file; máy khác chạy `dotnet run` sẽ thiếu `Web:CustomerOrigins` và API dừng khi khởi động | `.gitignore` chặn `appsettings.Development.json` (cố ý, tránh lọt cấu hình dev) | Bỏ file, đặt `Web:CustomerOrigins:0` bằng `dotnet user-secrets` và ghi vào README cùng các khóa khác | Cấu hình riêng máy dev luôn để ở User Secrets; kiểm `git check-ignore -v <file>` khi file mới không hiện trong `git status` |

## Xác thực – nền tảng: giờ VN, băm mật khẩu, token an toàn (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Test `Malformed_hash_fails_without_throwing("không-phải-base64")` đỏ: ném `FormatException` | `PasswordHasher.VerifyHashedPassword` của Identity giải Base64 bản băm mà không bắt lỗi; bản băm hỏng trong CSDL sẽ thành lỗi 500 khi đăng nhập | Bọc lời gọi trong `try/catch (FormatException)` ở `Pbkdf2PasswordHasher.Verify`, trả `PasswordCheckResult.Failed` | Với thư viện bên ngoài nhận dữ liệu lấy từ CSDL, luôn viết test cho dữ liệu hỏng |

## Nền CSDL – EF Core, migration, kiểm thử tích hợp (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Build lỗi `NU1903/NU1902: Package 'System.Security.Cryptography.Pkcs' 9.0.11 has a known high severity vulnerability` sau khi thêm `Microsoft.EntityFrameworkCore.SqlServer` 10.0.14 | Gói phụ thuộc gián tiếp: EF Core SqlServer → Microsoft.Data.SqlClient 6.1.7 → Pkcs 9.0.11 (có lỗ hổng, bản vá từ 10.0.13 cho nhánh 10.x) | Thêm `PackageVersion Include="System.Security.Cryptography.Pkcs" Version="10.0.14"` vào `Directory.Packages.props` (đã bật `CentralPackageTransitivePinningEnabled`) rồi restore lại; `dotnet list package --vulnerable --include-transitive` hết cảnh báo | Sau mỗi lần thêm hoặc nâng gói chính, chạy `dotnet build` và `dotnet list package --vulnerable --include-transitive`; vá gói gián tiếp bằng ghim phiên bản, không tắt cảnh báo |
| Test `TST_31` (DROP/ALTER bằng tài khoản ứng dụng) đỏ: mong đợi lỗi 229, nhận 3701 và 1088 | Khi thiếu quyền ALTER/DROP trên bảng, SQL Server trả lỗi "không tồn tại hoặc không có quyền" (3701, 1088) thay vì 229; quyền DDL thực sự đã bị chặn | Sửa mã lỗi mong đợi: CREATE → 262, ALTER → 1088, DROP → 3701; UPDATE/DELETE bảng nhật ký vẫn là 229 | Với quyền DDL, kiểm lỗi theo từng câu lệnh bằng cách chạy thử rồi đọc `SqlException.Number`, không đoán |
| Build lỗi `MA0051`, `CA1861`, `IDE0161`, `MA0048` trong file migration EF | Bộ phân tích áp cả lên mã migration do EF sinh | Thêm `Persistence/Migrations/.editorconfig` với `generated_code = true` | Thư mục migration luôn được đánh dấu mã sinh tự động |
| Build lỗi `CA1000` (static trên lớp generic), `MA0048` (nhiều lớp một file), `CA1711`, `MA0004` | Quy tắc "một kiểu một file", không static trên lớp generic; ConfigureAwait và tên `...Collection` không áp dụng cho ASP.NET Core/xunit | Tách static ra lớp không generic `SnakeCaseEnum`, mỗi converter một file; tắt `MA0004` ở `.editorconfig` gốc và `CA1711` ở `tests/.editorconfig` | Đặt tên và tách file đúng quy tắc ngay từ đầu |
| Lệnh tạo nhiều file bằng heredoc trong shell báo `unexpected EOF while looking for matching` và không tạo file nào | Lệnh dài chứa nhiều ngoặc kép/ngoặc đơn lồng nhau làm shell phân tích sai | Ghi từng file bằng công cụ ghi file; kiểm lại bằng `ls` trước khi build | Không gộp nhiều file lớn vào một lệnh shell |
| `dotnet ef database update` cảnh báo thao tác `ALTER DATABASE … READ_COMMITTED_SNAPSHOT` không chạy được trong transaction | Câu lệnh đó không được nằm trong transaction nên migration chạy từng phần | Vẫn chạy thành công (`suppressTransaction: true`); khuyến nghị tách thành migration riêng chỉ chứa thao tác này | Khi thêm thao tác ALTER DATABASE khác, đặt vào migration riêng |

## Hệ thống – dựng khung dự án (06/11/2026)

| Triệu chứng | Nguyên nhân | Cách sửa | Phòng tránh |
|---|---|---|---|
| Build lỗi `CA1707: Remove the underscores from member name …` ở project test | Tên test đặt theo mã yêu cầu (`AR_01_…`) có dấu gạch dưới, bộ phân tích `latest-recommended` cấm | Thêm `tests/.editorconfig` với `dotnet_diagnostic.CA1707.severity = none` (chỉ áp cho thư mục `tests/`) | Tên test dùng gạch dưới là quy ước của dự án (`docs/srs/07` TST-01); luôn đặt tắt rule này ở `tests/` |
| Build lỗi `MA0002: Use an overload that has a IEqualityComparer<string>` | Gọi `new HashSet<string>()` / `ToHashSet()` không truyền bộ so sánh chuỗi | Truyền `StringComparer.Ordinal` vào cả hai chỗ | So sánh chuỗi luôn nêu rõ `StringComparison`/`StringComparer` |
| Build lỗi `CS1012: Too many characters in character literal` ở dòng `Replace('\\', '/')` | Tạo file bằng heredoc của shell: ký tự gạch chéo ngược bị nuốt mất một dấu, nên literal ký tự thành `'\'` (sai) | Sửa lại dòng bằng công cụ chỉnh sửa file để có đủ hai dấu gạch chéo ngược `'\\'` | Không tạo file C# có gạch chéo ngược bằng heredoc/python trong shell; dùng công cụ ghi file trực tiếp rồi build ngay |
| Bộ cài `winget install Microsoft.DotNet.SDK.10` chạy mãi, không báo lỗi | Bộ cài đang chờ hộp thoại quyền quản trị (UAC) trên màn hình, chưa được bấm đồng ý | Bấm **Yes** ở hộp thoại UAC; kiểm lại bằng `dotnet --list-sdks` thấy `10.0.403` | Báo trước cho người dùng khi lệnh cần quyền quản trị; không đợi vô hạn |
| `ng new` / `ng generate` sinh thêm file thừa: `.vscode/`, `README.md`, `.gitignore`, `.editorconfig` trong `web/` | Mặc định của Angular CLI | Xóa các file đó (repo đã có `.gitignore` và `.editorconfig` ở gốc) trước khi commit | Sau mỗi lần chạy CLI sinh mã, chạy `git status` và xóa file thừa trước khi `git add` |
