using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Infrastructure.Caching;
using ObentoTensho.Infrastructure.Loyalty;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.Infrastructure.Orders;
using ObentoTensho.Infrastructure.Persistence;
using ObentoTensho.Infrastructure.Persistence.Repositories;
using ObentoTensho.Infrastructure.Reservations;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.Infrastructure.Spreadsheets;
using ObentoTensho.Infrastructure.Time;
using ObentoTensho.Infrastructure.Vouchers;

namespace ObentoTensho.Infrastructure;

public static class DependencyInjection
{
    private const int CommandTimeoutSeconds = 30;

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // FR-ARC-09: thiếu cấu hình bắt buộc thì dừng ngay khi khởi động
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings:Default.");
        }

        // DB-15: tự thử lại khi lỗi tạm thời, thời gian chờ lệnh 30 giây
        services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure().CommandTimeout(CommandTimeoutSeconds)));

        services.AddMemoryCache();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountEmailOutbox, AccountEmailOutbox>();
        services.AddScoped<IAuditTrail, EfAuditTrail>();
        services.AddScoped<ITableLayoutRepository, TableLayoutRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddSingleton<IPublicScheduleCache, MemoryPublicScheduleCache>();
        services.AddSingleton<IActionThrottle, MemoryActionThrottle>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IReservationEmailOutbox, ReservationEmailOutbox>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddScoped<ILoyaltyEmailOutbox, LoyaltyEmailOutbox>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IVoucherEmailOutbox, VoucherEmailOutbox>();
        services.AddSingleton<IVoucherSpreadsheet, ClosedXmlVoucherSpreadsheet>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddSingleton<IPromotionCache, MemoryPromotionCache>();
        services.AddScoped<IOrderEmailOutbox, OrderEmailOutbox>();
        services.AddSingleton<IMenuCache, MemoryMenuCache>();
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizerAdapter>();
        services.AddSingleton<IMenuSpreadsheet, ClosedXmlMenuSpreadsheet>();

        AddBusinessSettings(services, configuration);

        return services;
    }

    // Núm vặn nghiệp vụ (docs/srs/19 §3): sai giá trị thì dừng ngay khi khởi động
    private static void AddBusinessSettings(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReservationOptions>()
            .Bind(configuration.GetSection(ReservationOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Reservation không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<IReservationSettings>(sp => sp.GetRequiredService<IOptions<ReservationOptions>>().Value);

        services.AddOptions<LoyaltyOptions>()
            .Bind(configuration.GetSection(LoyaltyOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Loyalty không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<ILoyaltySettings>(sp => sp.GetRequiredService<IOptions<LoyaltyOptions>>().Value);

        services.AddOptions<VoucherOptions>()
            .Bind(configuration.GetSection(VoucherOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Voucher không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<IVoucherSettings>(sp => sp.GetRequiredService<IOptions<VoucherOptions>>().Value);

        services.AddOptions<OrderOptions>()
            .Bind(configuration.GetSection(OrderOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Order không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<IOrderSettings>(sp => sp.GetRequiredService<IOptions<OrderOptions>>().Value);
    }

    public static IServiceCollection AddSecurityFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();

        // Số vòng băm dưới mức tối thiểu thì dừng ngay khi khởi động
        services.AddOptions<PasswordHashingOptions>()
            .Bind(configuration.GetSection(PasswordHashingOptions.SectionName))
            .Validate(
                o => o.Iterations >= PasswordHashingOptions.MinimumIterations,
                $"Security:PasswordHashing:Iterations phải từ {PasswordHashingOptions.MinimumIterations} trở lên.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecureTokenService, SecureTokenService>();
        services.AddSingleton<ICommonPasswordList, EmbeddedCommonPasswordList>();

        // SEC-16: thiếu hoặc yếu khóa ký JWT thì dừng ngay khi khởi động
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Khóa Data Protection mã hóa khóa TOTP: máy chủ phải đặt DataProtection:KeysPath tới thư mục bền
        var dataProtection = services.AddDataProtection().SetApplicationName("ObentoTensho");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<IStaffChallengeTokens, DataProtectionStaffChallengeTokens>();
        services.AddSingleton<ITotpSecretProtector, DataProtectionTotpSecretProtector>();

        return services;
    }
}
