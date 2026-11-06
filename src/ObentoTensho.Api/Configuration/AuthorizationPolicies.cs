using Microsoft.AspNetCore.Authorization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Api.Configuration;

// Mỗi StaffCapability là một policy; danh sách vai trò lấy từ ma trận ở Core (một nguồn duy nhất)
public static class AuthorizationPolicies
{
    public const string Customer = "customer";
    private const string CapabilityPrefix = "staff:";

    public static string For(StaffCapability capability) => CapabilityPrefix + capability;

    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddPolicy(Customer, policy => policy
            .AddAuthenticationSchemes(AuthSchemes.Customer)
            .RequireAuthenticatedUser()
            .RequireRole(AuthPolicy.CustomerRole));

        foreach (var capability in Enum.GetValues<StaffCapability>())
        {
            var roles = StaffCapabilities.RolesFor(capability).Select(r => r.ToString()).ToArray();
            builder.AddPolicy(For(capability), policy => policy
                .AddAuthenticationSchemes(AuthSchemes.Staff)
                .RequireAuthenticatedUser()
                .RequireRole(roles));
        }

        // AR-09: mặc định từ chối; endpoint công khai phải ghi [AllowAnonymous]
        return builder.SetFallbackPolicy(new AuthorizationPolicyBuilder(AuthSchemes.Customer, AuthSchemes.Staff)
            .RequireAuthenticatedUser()
            .Build());
    }
}
