using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Staff;

public sealed record UpdateStaffRequest(string FullName, StaffRole Role);
