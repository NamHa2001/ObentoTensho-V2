using Microsoft.Extensions.Options;
using ObentoTensho.Api.Configuration;

namespace ObentoTensho.Api.Http;

public sealed class CustomerOriginFilter(IOptions<WebOriginsOptions> options) : TrustedOriginFilter
{
    protected override IEnumerable<string> AllowedOrigins => options.Value.CustomerOrigins;
}
