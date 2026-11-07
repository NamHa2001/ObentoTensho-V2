namespace ObentoTensho.Contracts.Dtos.Customers;

/// <summary>Khách yêu cầu xóa qua nhân viên (BR-CUS-05).</summary>
public sealed record CustomerDeletionRequest(string Reason);
