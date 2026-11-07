using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Verification.Domain;
using ObentoTensho.Infrastructure.Messaging;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Staff> StaffMembers => Set<Staff>();

    public DbSet<StaffRecoveryCode> StaffRecoveryCodes => Set<StaffRecoveryCode>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    public DbSet<TableArea> TableAreas => Set<TableArea>();

    public DbSet<DiningTable> Tables => Set<DiningTable>();

    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();

    public DbSet<SpecialHour> SpecialHours => Set<SpecialHour>();

    public DbSet<ServiceSlot> ServiceSlots => Set<ServiceSlot>();

    public DbSet<BlockedDate> BlockedDates => Set<BlockedDate>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ReservationStatusHistory> ReservationStatusHistory => Set<ReservationStatusHistory>();

    public DbSet<SlotDayCapacity> SlotDayCapacity => Set<SlotDayCapacity>();

    public DbSet<DailyCounter> DailyCounters => Set<DailyCounter>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>();

    public DbSet<MenuOption> Options => Set<MenuOption>();

    public DbSet<MenuItemOptionGroup> MenuItemOptionGroups => Set<MenuItemOptionGroup>();

    public DbSet<MenuItemTranslation> MenuItemTranslations => Set<MenuItemTranslation>();

    public DbSet<MenuCategoryTranslation> MenuCategoryTranslations => Set<MenuCategoryTranslation>();

    public DbSet<OptionGroupTranslation> OptionGroupTranslations => Set<OptionGroupTranslation>();

    public DbSet<OptionTranslation> OptionTranslations => Set<OptionTranslation>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // DB-01: datetime2(3), luôn UTC
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)").HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveColumnType("datetime2(3)").HaveConversion<NullableUtcDateTimeConverter>();

        // Giờ địa phương trong ngày (giờ mở cửa, ca) chỉ cần tới giây
        configurationBuilder.Properties<TimeOnly>().HaveColumnType("time(0)");
        configurationBuilder.Properties<TimeOnly?>().HaveColumnType("time(0)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
