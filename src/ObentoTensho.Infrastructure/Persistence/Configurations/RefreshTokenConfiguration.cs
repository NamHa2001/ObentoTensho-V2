using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Infrastructure.Persistence.Conversions;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        var reasons = string.Join(", ", SnakeCaseEnum.AllValues<RefreshTokenRevokedReason>().Select(v => $"'{v}'"));

        builder.ToTable("RefreshTokens", table =>
        {
            // Token thuộc đúng một chủ: khách hoặc nhân viên
            table.HasCheckConstraint(
                "CK_RefreshTokens_Owner",
                "(CASE WHEN [UserId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StaffId] IS NULL THEN 0 ELSE 1 END) = 1");
            table.HasCheckConstraint("CK_RefreshTokens_RevokedReason", $"[RevokedReason] IS NULL OR [RevokedReason] IN ({reasons})");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasColumnType("char(64)");
        builder.Property(t => t.RevokedReason)
            .HasConversion(new SnakeCaseEnumConverter<RefreshTokenRevokedReason>())
            .HasColumnType("varchar(30)");
        builder.Property(t => t.CreatedByIp).HasColumnType("varchar(45)");
        builder.Property(t => t.UserAgent).HasColumnType("nvarchar(300)");

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Staff>().WithMany().HasForeignKey(t => t.StaffId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.StaffId);
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.ExpiresAtUtc);
    }
}
