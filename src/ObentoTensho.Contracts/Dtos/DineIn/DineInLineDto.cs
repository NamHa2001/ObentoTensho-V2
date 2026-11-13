namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Một món trên phiếu bàn. <c>Round</c>: đợt gọi (1, 2, …); món đã hủy vẫn hiện (gạch) nhưng không tính tiền.</summary>
public sealed record DineInLineDto(
    long Id,
    int Round,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    IReadOnlyList<string> Options,
    string? Note,
    DateTime SentAtUtc,
    DateTime? ServedAtUtc,
    bool Voided,
    string? VoidReason);
