using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Api.Controllers.Public;

// Thông tin quán, mạng xã hội, chuyển khoản, SEO: chỉ các khóa đánh dấu công khai trong danh mục
[ApiController]
[Route("api/v1/public/settings")]
[AllowAnonymous]
public sealed class PublicSettingsController(ISettingsReader settings, IImageRepository images, ImageMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(settings.Public(), StringComparer.Ordinal);
        await AddImageUrlAsync(values, SettingKeys.RestaurantLogo, "restaurant.logoUrl", cancellationToken);
        await AddImageUrlAsync(values, SettingKeys.PaymentQrImageId, "payment.qrImageUrl", cancellationToken);
        return this.Data(values);
    }

    // Cài đặt chỉ lưu mã ảnh; trang khách cần địa chỉ để hiển thị logo và QR chuyển khoản
    private async Task AddImageUrlAsync(Dictionary<string, string> values, string key, string urlKey, CancellationToken cancellationToken)
    {
        if (values.TryGetValue(key, out var raw) && Guid.TryParse(raw, out var id)
            && await images.FindAsync(id, cancellationToken) is { } image
            && mapper.ToDto(image).Variants is { Count: > 0 } variants)
        {
            values[urlKey] = variants[^1].Url;
        }
    }
}
