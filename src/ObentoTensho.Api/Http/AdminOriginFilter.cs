using Microsoft.Extensions.Options;
using ObentoTensho.Api.Configuration;

namespace ObentoTensho.Api.Http;

// SEC-41: cookie phiên quản trị chỉ dùng được từ origin trang quản trị
public sealed class AdminOriginFilter(IOptions<WebOriginsOptions> options) : TrustedOriginFilter
{
    protected override IEnumerable<string> AllowedOrigins => options.Value.AdminOrigins;
}
