using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

public sealed record PaymentGroupRowDto(PaymentGroup Group, PaymentAmountsDto Amounts, IReadOnlyList<PaymentChannelRowDto> Channels);
