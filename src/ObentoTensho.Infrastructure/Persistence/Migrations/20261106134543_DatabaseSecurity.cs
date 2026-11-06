using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObentoTensho.Infrastructure.Persistence.Migrations
{
    // DB-09: đọc không chặn ghi (READ_COMMITTED_SNAPSHOT).
    // DB-10: vai trò ot_app_runtime cho tài khoản chạy ứng dụng: chỉ SELECT/INSERT/UPDATE/DELETE,
    // không có quyền DDL, và bị cấm UPDATE/DELETE trên bảng nhật ký bất biến.
    // Đăng nhập (login) của ứng dụng được tạo ở bước triển khai rồi thêm vào vai trò này.
    public partial class DatabaseSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;",
                suppressTransaction: true);

            migrationBuilder.Sql("IF DATABASE_PRINCIPAL_ID('ot_app_runtime') IS NULL CREATE ROLE [ot_app_runtime];");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [ot_app_runtime];");
            migrationBuilder.Sql("DENY UPDATE, DELETE ON OBJECT::dbo.AuditLogs TO [ot_app_runtime];");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF DATABASE_PRINCIPAL_ID('ot_app_runtime') IS NOT NULL DROP ROLE [ot_app_runtime];");

            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;",
                suppressTransaction: true);
        }
    }
}
