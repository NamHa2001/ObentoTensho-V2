using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Staff;

/// <summary>Frontend dùng <c>Capabilities</c> để ẩn/hiện menu; server luôn kiểm lại.</summary>
public sealed record StaffMeDto(Guid Id, string FullName, string Email, StaffRole Role, IReadOnlyList<StaffCapability> Capabilities, bool TotpEnabled);
