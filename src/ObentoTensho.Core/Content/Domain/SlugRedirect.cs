namespace ObentoTensho.Core.Content.Domain;

// BR-CNT-41: slug cũ của nội dung đã công bố, trả 301 về slug mới
public sealed class SlugRedirect
{
    public const string NewsEntity = "news";
    public const string MenuItemEntity = "menu_item";
    public const string PromotionEntity = "promotion";

    public required string EntityType { get; init; }

    public required string OldSlug { get; init; }

    public required int EntityId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
