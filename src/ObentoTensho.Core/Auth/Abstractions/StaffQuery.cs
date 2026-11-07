using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Abstractions;

public sealed record StaffQuery(string? Search, StaffRole? Role, bool? IsActive, int Page, int PageSize);
