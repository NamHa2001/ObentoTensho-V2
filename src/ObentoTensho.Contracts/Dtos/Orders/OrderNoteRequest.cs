namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Văn bản thuần: ghi chú cuộc gọi xác nhận, ghi chú hoàn tiền (bắt buộc với hoàn tiền).</summary>
public sealed record OrderNoteRequest(string? Note);
