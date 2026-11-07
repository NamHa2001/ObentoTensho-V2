namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Nhãn hiển thị trên thẻ món.</summary>
public sealed record MenuLabels(bool IsNew, bool IsBestseller, bool IsFeatured, bool IsVegetarian, bool IsSpicy);
