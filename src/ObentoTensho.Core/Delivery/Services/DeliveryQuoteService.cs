using ObentoTensho.Contracts.Dtos.Delivery;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Delivery.Abstractions;
using ObentoTensho.Core.Delivery.Domain;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Delivery.Services;

// Phí ship tính ở server từ quãng đường lái xe thật (Google Routes); dùng cho cả xem trước và lúc tạo đơn
public sealed class DeliveryQuoteService(IRouteDistance routes, ISettingsReader settings)
{
    public const int AddressMinLength = 8;
    public const int AddressMaxLength = 300;
    private static readonly char[] Markup = ['<', '>'];

    public bool Enabled => settings.GetBool(SettingKeys.DeliveryEnabled);

    public async Task<Result<DeliveryQuoteDto>> QuoteAsync(string? rawAddress, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.DeliveryDisabled);
        }

        var address = rawAddress?.Trim() ?? string.Empty;
        if (address.Length is < AddressMinLength or > AddressMaxLength || address.IndexOfAny(Markup) >= 0)
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["address"] = [address.Length > AddressMaxLength ? FieldErrorCodes.TooLong : FieldErrorCodes.InvalidFormat],
            });
        }

        var origin = settings.Get(SettingKeys.DeliveryOrigin) is { Length: > 0 } custom ? custom : settings.Get(SettingKeys.RestaurantAddress);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.DeliveryUnavailable);
        }

        var route = await routes.DrivingAsync(origin, address, cancellationToken);
        if (route.Outcome != RouteOutcome.Found)
        {
            return Result.Failure<DeliveryQuoteDto>(route.Outcome == RouteOutcome.NotFound ? ErrorCodes.AddressNotFound : ErrorCodes.DeliveryUnavailable);
        }

        if (route.Meters > settings.GetInt(SettingKeys.DeliveryMaxKm) * 1000)
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.DeliveryTooFar);
        }

        var freeKm = settings.GetInt(SettingKeys.DeliveryFreeKm);
        var feePerKm = settings.GetInt(SettingKeys.DeliveryFeePerKm);
        return Result.Success(new DeliveryQuoteDto(address, DeliveryFee.Kilometers(route.Meters), DeliveryFee.Compute(route.Meters, freeKm, feePerKm), freeKm, feePerKm));
    }
}
