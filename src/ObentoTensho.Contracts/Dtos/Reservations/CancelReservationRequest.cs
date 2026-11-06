namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Khách hủy bằng token trong liên kết email; khách đã đăng nhập đúng chủ thì không cần token.</summary>
public sealed record CancelReservationRequest(string? Token, string? Reason);
