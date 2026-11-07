using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.UnitTests.Content;

// BR-CNT-01: chỉ bài xuất bản, tới giờ đăng, chưa xóa
public sealed class NewsArticleTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(NewsStatus.Published, 0, false, true)]
    [InlineData(NewsStatus.Published, -60, false, true)]
    [InlineData(NewsStatus.Published, 1, false, false)]
    [InlineData(NewsStatus.Published, null, false, false)]
    [InlineData(NewsStatus.Published, -60, true, false)]
    [InlineData(NewsStatus.Draft, -60, false, false)]
    [InlineData(NewsStatus.Hidden, -60, false, false)]
    public void Hien_cong_khai(NewsStatus status, int? minutes, bool deleted, bool visible)
    {
        var article = new NewsArticle
        {
            Title = "x",
            Slug = "x",
            TitleSearch = "x",
            ContentHtml = "x",
            CreatedAtUtc = Now,
            Status = status,
            PublishedAtUtc = minutes is { } m ? Now.AddMinutes(m) : null,
            IsDeleted = deleted,
        };

        Assert.Equal(visible, article.IsVisible(Now));
    }
}
