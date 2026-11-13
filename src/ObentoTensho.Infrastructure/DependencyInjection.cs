using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Maintenance.Abstractions;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Invoices.Abstractions;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Infrastructure.Caching;
using ObentoTensho.Infrastructure.Content;
using ObentoTensho.Infrastructure.Loyalty;
using ObentoTensho.Infrastructure.Media;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.Infrastructure.Orders;
using ObentoTensho.Infrastructure.Persistence;
using ObentoTensho.Infrastructure.Persistence.Repositories;
using ObentoTensho.Infrastructure.Realtime;
using ObentoTensho.Infrastructure.Reservations;
using ObentoTensho.Infrastructure.Security;
using ObentoTensho.Infrastructure.Settings;
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
        AddPublicCaches(services);
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        AddRealtime(services);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountEmailOutbox, AccountEmailOutbox>();
        services.AddScoped<IAuditTrail, EfAuditTrail>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ITableLayoutRepository, TableLayoutRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddSingleton<IActionThrottle, MemoryActionThrottle>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IReservationEmailOutbox, ReservationEmailOutbox>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
        services.AddScoped<ILoyaltyEmailOutbox, LoyaltyEmailOutbox>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IVoucherEmailOutbox, VoucherEmailOutbox>();
        services.AddSingleton<IVoucherSpreadsheet, ClosedXmlVoucherSpreadsheet>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        AddContent(services);
        services.AddScoped<IStaticPageRepository, StaticPageRepository>();
        services.AddSingleton<IImageProcessor, NetVipsImageProcessor>();
        services.AddSingleton<IImageFileStore, LocalImageFileStore>();
        services.AddScoped<IOrderEmailOutbox, OrderEmailOutbox>();
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
        services.AddSingleton<IReservationSettings, StoreReservationSettings>();

        services.AddOptions<LoyaltyOptions>()
            .Bind(configuration.GetSection(LoyaltyOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Loyalty không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<ILoyaltySettings, StoreLoyaltySettings>();

        services.AddOptions<VoucherOptions>()
            .Bind(configuration.GetSection(VoucherOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Voucher không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<IVoucherSettings, StoreVoucherSettings>();

        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName));
        services.AddOptions<ContentOptions>().Bind(configuration.GetSection(ContentOptions.SectionName));
        services.AddOptions<ScheduleCacheOptions>().Bind(configuration.GetSection(ScheduleCacheOptions.SectionName));
        services.AddSingleton<IContentSettings>(sp => sp.GetRequiredService<IOptions<ContentOptions>>().Value);

        services.AddOptions<OrderOptions>()
            .Bind(configuration.GetSection(OrderOptions.SectionName))
            .Validate(o => o.IsValid(), "Cấu hình Order không hợp lệ (xem docs/srs/19 §3).")
            .ValidateOnStart();
        services.AddSingleton<IOrderSettings, StoreOrderSettings>();

        services.AddSingleton<SettingsStore>();
        services.AddSingleton<ISettingsReader>(sp => sp.GetRequiredService<SettingsStore>());
        services.AddSingleton<ISettingsSnapshot>(sp => sp.GetRequiredService<SettingsStore>());
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IStatsRepository, StatsRepository>();
        services.AddSingleton<IDashboardCache, MemoryDashboardCache>();
        services.AddHostedService<SettingsRefreshWorker>();
    }

    public static IServiceCollection AddSecurityFoundation(this IServiceCollection services, IConfiguration configuration, bool isProduction = false)
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
        services.AddSingleton<IDisposableEmailDomains, EmbeddedDisposableEmailDomains>();

        // SEC-16: thiếu hoặc yếu khóa ký JWT thì dừng ngay khi khởi động
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<ISessionDenylist, MemorySessionDenylist>();

        // Khóa Data Protection mã hóa khóa TOTP: máy chủ phải đặt DataProtection:KeysPath tới thư mục bền
        var dataProtection = services.AddDataProtection().SetApplicationName("ObentoTensho");
        var keysPath = configuration["DataProtection:KeysPath"];

        // SEC-16: khóa chỉ trong bộ nhớ thì khởi động lại là mất toàn bộ 2FA nhân viên và liên kết hủy bản tin
        if (isProduction && string.IsNullOrWhiteSpace(keysPath))
        {
            throw new InvalidOperationException("Môi trường Production phải cấu hình DataProtection:KeysPath tới thư mục bền.");
        }

        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<IStaffChallengeTokens, DataProtectionStaffChallengeTokens>();
        services.AddSingleton<ITotpSecretProtector, DataProtectionTotpSecretProtector>();

        return services;
    }

    // Sự kiện realtime gom theo phạm vi, phát sau commit (docs/srs/06 §B.2)
    // PERF-11: cache ngắn cho dữ liệu công khai, xóa ngay khi quản trị sửa
    private static void AddPublicCaches(IServiceCollection services)
    {
        services.AddSingleton<IPublicScheduleCache, MemoryPublicScheduleCache>();
        services.AddSingleton<IPublicContentCache, MemoryPublicContentCache>();
        services.AddSingleton<IPublicReviewCache, MemoryPublicReviewCache>();
        services.AddSingleton<IPromotionCache, MemoryPromotionCache>();
        services.AddSingleton<IMenuCache, MemoryMenuCache>();
    }

    private static void AddRealtime(IServiceCollection services)
    {
        services.AddSingleton<RealtimeFeed>();
        services.AddScoped<PendingRealtimeEvents>();
        services.AddScoped<IRealtimeEvents>(sp => sp.GetRequiredService<PendingRealtimeEvents>());
    }

    private static void AddContent(IServiceCollection services)
    {
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<INewsRepository, NewsRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<ITranslationRepository, TranslationRepository>();
        services.AddScoped<ISlugRedirectRepository, SlugRedirectRepository>();
        services.AddScoped<IDataRetentionRepository, DataRetentionRepository>();
        services.AddSingleton<ITranslationCache, MemoryTranslationCache>();
        services.AddScoped<IContactEmailOutbox, ContactEmailOutbox>();
        services.AddScoped<INewsletterRepository, NewsletterRepository>();
        services.AddScoped<INewsletterEmails, NewsletterEmails>();
        services.AddSingleton<INewsletterTokenProtector, DataProtectionNewsletterTokenProtector>();
        services.AddSingleton<ITableSpreadsheet, ClosedXmlTableSpreadsheet>();
        services.AddSingleton<IReportPdf, Reports.QuestPdfReport>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDataExportRepository, DataExportRepository>();
        services.AddScoped<IAdminEmailOutbox, AdminEmailOutbox>();
    }
}
