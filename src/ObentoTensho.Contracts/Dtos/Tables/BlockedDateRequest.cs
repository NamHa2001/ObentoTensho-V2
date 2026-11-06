using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Tables;

public sealed record BlockedDateRequest(DateOnly Date, BlockScope AppliesTo, TimeOnly? Start, TimeOnly? End, string? Reason);
