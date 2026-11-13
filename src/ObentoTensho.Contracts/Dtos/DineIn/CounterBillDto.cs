namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Đơn mang về tại quầy đang mở (chưa thanh toán, hoặc đã thanh toán nhưng còn món chưa ra).</summary>
public sealed record CounterBillDto(Guid Id, string Code, string CustomerName, DateTime OpenedAtUtc, decimal SubTotal, int UnservedCount, bool Paid);
