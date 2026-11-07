using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.UnitTests.Content;

// BR-CNT-14: tên rút gọn; BR-CNT-16: thời hạn sửa
public sealed class ReviewTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Nguyễn Văn An", "Nguyễn A.")]
    [InlineData("  Trần   bình ", "Trần B.")]
    [InlineData("An", "An")]
    [InlineData("Khách đã xóa tài khoản", "Khách đã xóa tài khoản")]
    [InlineData("", "")]
    public void Ten_rut_gon(string fullName, string expected) =>
        Assert.Equal(expected, Review.ShortAuthorName(fullName));

    [Theory]
    [InlineData(ReviewStatus.Pending, 200, true)]
    [InlineData(ReviewStatus.Approved, 23, true)]
    [InlineData(ReviewStatus.Approved, 24, false)]
    [InlineData(ReviewStatus.Rejected, 25, false)]
    public void Thoi_han_sua(ReviewStatus status, int hoursAgo, bool canEdit)
    {
        var review = new Review { UserId = Guid.Empty, OrderId = Guid.Empty, Rating = 5, Status = status, CreatedAtUtc = Now.AddHours(-hoursAgo) };

        Assert.Equal(canEdit, review.CanEdit(Now));
    }
}
