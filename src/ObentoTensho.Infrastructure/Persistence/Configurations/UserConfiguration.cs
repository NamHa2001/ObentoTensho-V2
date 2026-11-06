using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table =>
        {
            // Email lưu chữ thường; so sánh nhị phân vì collation mặc định không phân biệt hoa thường
            table.HasCheckConstraint(
                "CK_Users_Email_Lowercase",
                "[Email] COLLATE Latin1_General_BIN2 = LOWER([Email]) COLLATE Latin1_General_BIN2");
            table.HasCheckConstraint("CK_Users_Points", "[CurrentPoints] >= 0 AND [PointsDebt] >= 0 AND [AccumulatedPoints] >= 0");
            table.HasCheckConstraint("CK_Users_MemberTier", "[MemberTier] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_Users_FailedLoginCount", "[FailedLoginCount] >= 0");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.FullName).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(u => u.Email).HasColumnType("varchar(150)").IsRequired();
        builder.Property(u => u.EmailCanonical).HasColumnType("varchar(150)").IsRequired();
        builder.Property(u => u.PhoneNumber).HasColumnType("varchar(15)");
        builder.Property(u => u.PasswordHash).HasColumnType("varchar(256)");
        builder.Property(u => u.PendingEmail).HasColumnType("varchar(150)");
        builder.Property(u => u.PendingEmailTokenHash).HasColumnType("char(64)");
        builder.Property(u => u.EmailVerifyTokenHash).HasColumnType("char(64)");
        builder.Property(u => u.ResetPasswordTokenHash).HasColumnType("char(64)");
        builder.Property(u => u.SecurityStamp).HasColumnType("char(32)").IsRequired();
        builder.Property(u => u.PreferredLang).HasColumnType("char(2)").HasDefaultValue("vi").ValueGeneratedNever();

        builder.Property(u => u.MemberTier).HasConversion<byte>().HasColumnType("tinyint")
            .HasDefaultValue(MemberTier.Bronze).ValueGeneratedNever();
        builder.Property(u => u.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(u => u.RowVersion).IsRowVersion();

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.EmailCanonical);
        builder.HasIndex(u => u.PhoneNumber);
    }
}
