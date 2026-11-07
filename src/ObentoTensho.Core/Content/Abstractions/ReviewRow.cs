using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public sealed record ReviewRow(Review Review, string FullName, string OrderCode, string? ItemName);
