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
web/      workspace Angular
deploy/   Docker, Compose, Nginx, script
```

## Cấu hình và bí mật

Không đưa mật khẩu, khóa API hay chuỗi kết nối thật vào repo. Giá trị thật đặt trong User Secrets (máy phát triển) hoặc biến môi trường (máy chủ).
