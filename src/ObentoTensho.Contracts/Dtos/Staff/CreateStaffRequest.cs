using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Staff;

/// <summary>Tạo nhân viên: không có mật khẩu, nhân viên tự đặt qua liên kết mời (BR-STF-06).</summary>
public sealed record CreateStaffRequest(string FullName, string Email, StaffRole Role);
