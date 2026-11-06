using Microsoft.AspNetCore.Authorization;
using ObentoTensho.Api.Configuration;

namespace ObentoTensho.Api.Http;

/// <summary>Endpoint tài khoản khách: chỉ nhận access token khách.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireCustomerAttribute : AuthorizeAttribute
{
    public RequireCustomerAttribute()
    {
        Policy = AuthorizationPolicies.Customer;
        AuthenticationSchemes = AuthSchemes.Customer;
    }
}
