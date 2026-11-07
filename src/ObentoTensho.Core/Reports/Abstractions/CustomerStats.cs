using ObentoTensho.Contracts.Dtos.Reports;

namespace ObentoTensho.Core.Reports.Abstractions;

public sealed record CustomerStats(IReadOnlyList<TierCountDto> Tiers, int ActiveCustomers, int ReturningCustomers, IReadOnlyList<TopCustomerDto> TopCustomers);
