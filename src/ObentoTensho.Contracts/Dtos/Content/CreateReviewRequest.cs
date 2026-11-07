namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Đánh giá một món trong đơn hoàn thành; <c>MenuItemId</c> trống ⇒ đánh giá cả đơn.</summary>
public sealed record CreateReviewRequest(Guid OrderId, int? MenuItemId, int Rating, string? Comment);
