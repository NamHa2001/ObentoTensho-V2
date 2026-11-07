namespace ObentoTensho.Contracts.Dtos.Reports;

public sealed record TopCustomerDto(Guid Id, string FullName, int Orders, decimal Spent);
