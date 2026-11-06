using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        var maxStatus = (byte)Enum.GetValues<TableStatus>().Max();

        builder.ToTable("Tables", table =>
        {
            table.HasCheckConstraint("CK_Tables_Capacity", "[MinCapacity] >= 1 AND [MaxCapacity] >= [MinCapacity]");
            table.HasCheckConstraint("CK_Tables_CurrentStatus", $"[CurrentStatus] BETWEEN 0 AND {maxStatus}");
            table.HasCheckConstraint("CK_Tables_Layout", "[PosX] >= 0 AND [PosY] >= 0 AND [Width] >= 0 AND [Height] >= 0 AND [Rotation] BETWEEN 0 AND 359");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasColumnName("TableName").HasColumnType("nvarchar(50)").IsRequired();
        builder.Property(t => t.CurrentStatus).HasConversion<byte>().HasColumnType("tinyint")
            .HasDefaultValue(TableStatus.Available).ValueGeneratedNever();
        builder.Property(t => t.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(t => t.RowVersion).IsRowVersion();

        // BR-TBL-06, BR-TBL-14: không xóa dây chuyền khu vực → bàn
        builder.HasOne<TableArea>().WithMany().HasForeignKey(t => t.AreaId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(t => new { t.AreaId, t.Name }).IsUnique();
    }
}
