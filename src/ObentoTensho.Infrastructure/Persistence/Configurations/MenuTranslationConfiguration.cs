using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

// docs/srs/08 §2 "Bảng bản dịch": mỗi loại một bảng, PK (EntityId, Lang), chỉ en/ja; xóa thực thể gốc ⇒ xóa bản dịch
internal static class MenuTranslationConfiguration
{
    public static void Configure<T, TOwner>(EntityTypeBuilder<T> builder, string table, string idColumn, bool hasDescriptions)
        where T : MenuTranslation
        where TOwner : class
    {
        builder.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_Lang", "[Lang] IN ('en', 'ja')");
            t.HasCheckConstraint($"CK_{table}_ReviewStatus", "[ReviewStatus] BETWEEN 0 AND 1");
        });

        builder.HasKey(x => new { x.EntityId, x.Lang });
        builder.Property(x => x.EntityId).HasColumnName(idColumn);
        builder.Property(x => x.Lang).HasColumnType("char(2)");
        builder.Property(x => x.Name).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(x => x.SourceHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.ReviewStatus).HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(TranslationStatus.Draft).ValueGeneratedNever();
        if (hasDescriptions)
        {
            builder.Property(x => x.ShortDesc).HasColumnType("nvarchar(200)");
            builder.Property(x => x.FullDescHtml).HasColumnType("nvarchar(max)");
        }
        else
        {
            builder.Ignore(x => x.ShortDesc);
            builder.Ignore(x => x.FullDescHtml);
        }

        builder.HasOne<TOwner>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
    }
}
