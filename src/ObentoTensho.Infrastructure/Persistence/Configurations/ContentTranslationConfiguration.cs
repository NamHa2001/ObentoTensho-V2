using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

// docs/srs/08 §2 "Bảng bản dịch": mỗi loại một bảng, PK (EntityId, Lang), chỉ en/ja; xóa nội dung gốc ⇒ xóa bản dịch.
// Chỉ ánh xạ các cột mà bản Việt có; độ dài theo cột bản Việt
internal static class ContentTranslationConfiguration
{
    [Flags]
    public enum Columns
    {
        None = 0,
        Summary = 1,
        Body = 2,
        Cta = 4,
        Meta = 8,
    }

    public static void Configure<T, TOwner>(EntityTypeBuilder<T> builder, string table, string idColumn, int titleLength, int summaryLength, Columns columns, string titleColumn = "Title")
        where T : ContentTranslation
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
        builder.Property(x => x.Title).HasColumnName(titleColumn).HasColumnType($"nvarchar({titleLength})").IsRequired();
        builder.Property(x => x.SourceHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.ReviewStatus).HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(TranslationStatus.Draft).ValueGeneratedNever();
        Optional(builder, columns.HasFlag(Columns.Summary), nameof(ContentTranslation.Summary), $"nvarchar({summaryLength})");
        Optional(builder, columns.HasFlag(Columns.Body), nameof(ContentTranslation.BodyHtml), "nvarchar(max)");
        Optional(builder, columns.HasFlag(Columns.Cta), nameof(ContentTranslation.CtaText), "nvarchar(100)");
        Optional(builder, columns.HasFlag(Columns.Meta), nameof(ContentTranslation.MetaTitle), "nvarchar(200)");
        Optional(builder, columns.HasFlag(Columns.Meta), nameof(ContentTranslation.MetaDescription), "nvarchar(500)");
        builder.HasOne<TOwner>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void Optional<T>(EntityTypeBuilder<T> builder, bool mapped, string property, string type)
        where T : ContentTranslation
    {
        if (mapped)
        {
            builder.Property<string?>(property).HasColumnType(type);
        }
        else
        {
            builder.Ignore(property);
        }
    }
}
