using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Verification.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        var maxPurpose = (byte)Enum.GetValues<OtpPurpose>().Max();

        builder.ToTable("OtpChallenges", table =>
        {
            table.HasCheckConstraint("CK_OtpChallenges_Purpose", $"[Purpose] BETWEEN 0 AND {maxPurpose}");
            table.HasCheckConstraint("CK_OtpChallenges_Attempts", "[Attempts] >= 0");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Purpose).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(c => c.TargetHash).HasColumnType("char(64)").IsRequired();
        builder.Property(c => c.CodeHash).HasColumnType("char(64)").IsRequired();
        builder.Property(c => c.Attempts).HasColumnType("tinyint");
        builder.Property(c => c.IpHash).HasColumnType("char(64)");

        builder.HasIndex(c => new { c.TargetHash, c.CreatedAtUtc });
        builder.HasIndex(c => new { c.IpHash, c.CreatedAtUtc });
        builder.HasIndex(c => c.CreatedAtUtc);
    }
}
