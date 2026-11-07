using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Configurations;

internal sealed class UserVoucherConfiguration : IEntityTypeConfiguration<UserVoucher>
{
    public void Configure(EntityTypeBuilder<UserVoucher> builder)
    {
        builder.ToTable("UserVouchers", table => table.HasCheckConstraint("CK_UserVouchers_UsedCount", "[UsedCount] >= 0"));
        builder.HasKey(u => u.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Voucher>().WithMany().HasForeignKey(u => u.VoucherId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(u => new { u.VoucherId, u.UserId }).IsUnique();
        builder.HasIndex(u => u.UserId);
    }
}
