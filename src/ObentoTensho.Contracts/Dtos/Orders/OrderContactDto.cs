namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Liên hệ đầy đủ — chỉ trả khi thu ngân/quản lý bấm "Hiện liên hệ" để gọi xác nhận (có audit).</summary>
public sealed record OrderContactDto(string Phone, string Email);
