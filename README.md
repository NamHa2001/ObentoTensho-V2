# Obento Tensho

Website nhà hàng **Obento Tensho (お弁当 天晟)**: giới thiệu, thực đơn, đặt bàn, đặt món mang về, tài khoản thành viên tích điểm, tin tức và khuyến mãi; trang quản trị cho nhân viên.

Đây là bản làm mới hoàn toàn, đang trong giai đoạn khởi động. Chưa có bản chạy được.

## Công nghệ (dự kiến)

- Backend: .NET 10, ASP.NET Core Web API, EF Core, SQL Server, Redis (Valkey), SignalR, Hangfire
- Giao diện: Angular (khách: SSR + PWA; quản trị: SPA), đa ngôn ngữ Việt, Anh, Nhật
- Triển khai: Docker, Nginx

## Cấu trúc thư mục

```
src/      mã backend .NET
tests/    kiểm thử backend
web/      workspace Angular (projects/customer, projects/admin)
deploy/   Docker, Compose, Nginx, script
```

## Build và kiểm thử

Yêu cầu: .NET SDK 10 (ghim ở `global.json`), Node.js 24, Angular CLI 21.

```
dotnet build ObentoTensho.sln
dotnet test ObentoTensho.sln

cd web
npm ci
npx ng build customer
npx ng build admin
npx ng test customer --watch=false
```

## Kiểm thử tích hợp (CSDL thật)

Test tích hợp tạo lại CSDL `ObentoTensho_V2_Test` rồi chạy migration. Mặc định dùng SQL Server cục bộ (Windows Authentication). Máy khác hoặc CI đặt biến `OBENTO_TEST_SQL` là chuỗi kết nối tới máy chủ (không có tên CSDL).

Áp migration cho CSDL phát triển (chuỗi kết nối qua biến môi trường, không đưa vào mã):

```
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/ObentoTensho.Infrastructure
```

## Cấu hình và bí mật

Không đưa mật khẩu, khóa API hay chuỗi kết nối thật vào repo. Giá trị thật đặt trong User Secrets (máy phát triển) hoặc biến môi trường (máy chủ).

API không khởi động nếu thiếu cấu hình bắt buộc hoặc khóa ký JWT yếu (dưới 256 bit, khóa mẫu). Thiết lập máy phát triển:

```
cd src/ObentoTensho.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=ObentoTensho_V2_Dev;Integrated Security=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:ActiveKeyId" "dev-1"
dotnet user-secrets set "Jwt:SigningKeys:dev-1" "<khóa Base64 ngẫu nhiên 32 byte, ví dụ: openssl rand -base64 32>"
dotnet user-secrets set "Web:CustomerOrigins:0" "http://localhost:4200"
dotnet user-secrets set "Web:AdminOrigins:0" "http://localhost:4201"
dotnet run
```

Máy chủ dùng biến môi trường tương ứng (`ConnectionStrings__Default`, `Jwt__ActiveKeyId`, `Jwt__SigningKeys__<kid>`, `Web__CustomerOrigins__0`, `Web__AdminOrigins__0`) và bắt buộc đặt `DataProtection__KeysPath` tới một thư mục bền (volume riêng, có sao lưu): khóa trong thư mục này giải mã khóa 2FA của nhân viên, mất thư mục thì mọi nhân viên phải thiết lập lại 2FA.

Tài khoản SuperAdmin đầu tiên: khi hệ thống chưa có SuperAdmin nào, đặt `Seed__SuperAdmin__Email`, `Seed__SuperAdmin__FullName`, `Seed__SuperAdmin__Password` rồi khởi động API. Lần đăng nhập đầu sẽ bắt đổi mật khẩu và thiết lập 2FA; sau đó xóa các biến này. Đổi khóa ký: thêm khóa mới vào `Jwt:SigningKeys`, chuyển `Jwt:ActiveKeyId` sang khóa mới, giữ khóa cũ thêm ít nhất 15 phút rồi mới xóa.
