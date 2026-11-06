namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Gán bàn; <c>TableId</c> null để bỏ gán.</summary>
public sealed record AssignTableRequest(int? TableId);
