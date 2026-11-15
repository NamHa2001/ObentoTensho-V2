using System.Globalization;
using ObentoTensho.Contracts.Dtos.Delivery;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Delivery.Abstractions;
using ObentoTensho.Core.Delivery.Domain;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Delivery.Services;

// Phí ship tính ở server từ quãng đường lái xe thật (Google Routes, chưa có khóa thì OpenStreetMap); dùng cho cả xem trước và lúc tạo đơn.
// Khách ghim vị trí trên bản đồ thì tính tới đúng điểm ghim; không ghim thì tìm theo địa chỉ chữ.
public sealed class DeliveryQuoteService(IRouteDistance routes, ISettingsReader settings)
{
    public const int AddressMinLength = 8;
    public const int AddressMaxLength = 300;
    private static readonly char[] Markup = ['<', '>'];

    public bool Enabled => settings.GetBool(SettingKeys.DeliveryEnabled);

    public async Task<Result<DeliveryQuoteDto>> QuoteAsync(string? rawAddress, double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.DeliveryDisabled);
        }

        var address = rawAddress?.Trim() ?? string.Empty;
        if (Validate(address, latitude, longitude) is { } errors)
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.ValidationFailed, errors);
        }

        var origin = settings.Get(SettingKeys.DeliveryOrigin) is { Length: > 0 } custom ? custom : settings.Get(SettingKeys.RestaurantAddress);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Result.Failure<DeliveryQuoteDto>(ErrorCodes.DeliveryUnavailable);
        }

        var destination = latitude is { } lat && longitude is { } lng ? string.Create(CultureInfo.InvariantCulture, $"{lat:F6},{lng:F6}") : address;
        var route = await routes.DrivingAsync(origin, destination, cancellationToken);
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
        return Result.Success(new DeliveryQuoteDto(address, DeliveryFee.Kilometers(route.Meters), DeliveryFee.Compute(route.Meters, freeKm, feePerKm), freeKm, feePerKm, latitude, longitude));
    }

    // Điểm ghim phải đủ cả vĩ độ lẫn kinh độ và nằm trong lãnh thổ Việt Nam
    private static Dictionary<string, string[]>? Validate(string address, double? latitude, double? longitude)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (address.Length is < AddressMinLength or > AddressMaxLength || address.IndexOfAny(Markup) >= 0)
        {
            errors["address"] = [address.Length > AddressMaxLength ? FieldErrorCodes.TooLong : FieldErrorCodes.InvalidFormat];
        }

        if (latitude.HasValue != longitude.HasValue || latitude is < 8 or > 24 || longitude is < 102 or > 110)
        {
            errors["location"] = [FieldErrorCodes.OutOfRange];
        }

        return errors.Count == 0 ? null : errors;
    }
}
