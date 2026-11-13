namespace ObentoTensho.Contracts.Dtos.DineIn;

public sealed record DineInKitchenLineDto(long Id, int Round, string Name, int Quantity, IReadOnlyList<string> Options, string? Note, DateTime SentAtUtc);
