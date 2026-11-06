using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        // DB-02: CHECK của cột Role sinh từ enum nên không thể lệch với mã
        var roles = string.Join(", ", Enum.GetNames<StaffRole>().Select(name => $"'{name}'"));

        builder.ToTable("Staff", table =>
        {
            table.HasCheckConstraint("CK_Staff_Role", $"[Role] IN ({roles})");
            table.HasCheckConstraint(
                "CK_Staff_Email_Lowercase",
                "[Email] COLLATE Latin1_General_BIN2 = LOWER([Email]) COLLATE Latin1_General_BIN2");
            table.HasCheckConstraint("CK_Staff_FailedLoginCount", "[FailedLoginCount] >= 0");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.FullName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(s => s.Email).HasColumnType("varchar(150)").IsRequired();
        builder.Property(s => s.PasswordHash).HasColumnType("varchar(256)");
        builder.Property(s => s.Role).HasConversion<string>().HasColumnType("varchar(30)").IsRequired();
        builder.Property(s => s.InviteTokenHash).HasColumnType("char(64)");
        builder.Property(s => s.SecurityStamp).HasColumnType("char(32)").IsRequired();
        builder.Property(s => s.TotpSecretEncrypted).HasColumnType("varbinary(256)");

        builder.Property(s => s.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(s => s.MustChangePassword).HasDefaultValue(true).ValueGeneratedNever();

        builder.HasIndex(s => s.Email).IsUnique();
    }
}
