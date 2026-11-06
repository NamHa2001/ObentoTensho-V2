using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Configuration;

public static class AuthConfiguration
{
    public const string CustomerCorsPolicy = "customer";

    public static IServiceCollection AddCustomerAuth(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<CustomerSessionFactory>();
        services.AddScoped<CustomerSessionService>();
        services.AddScoped<CustomerRegistrationService>();
        services.AddScoped<AccessTokenSessionValidator>();
        services.AddScoped<TrustedOriginFilter>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => ConfigureBearer(bearer, jwt.Value));

        // AR-09: mặc định từ chối; endpoint công khai phải ghi [AllowAnonymous]
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    public static IServiceCollection AddWebOrigins(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebOriginsOptions>()
            .Bind(configuration.GetSection(WebOriginsOptions.SectionName))
            .Validate(o => o.Validate() is null, "Cấu hình Web:CustomerOrigins không hợp lệ.")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<WebOriginsOptions>>((cors, web) => cors.AddPolicy(CustomerCorsPolicy, policy => policy
                .WithOrigins([.. web.Value.CustomerOrigins])
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithHeaders("Authorization", "Content-Type", TrustedOriginFilter.ClientHeader, "Idempotency-Key", "If-Match")
                .AllowCredentials()));

        return services;
    }

    private static void ConfigureBearer(JwtBearerOptions bearer, JwtOptions jwt)
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.CustomerAudience,
            IssuerSigningKeys = jwt.GetValidationKeys().ToList(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = JwtOptions.ClockSkew,
            NameClaimType = AccessTokenClaims.Subject,
            RoleClaimType = AccessTokenClaims.Role,
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidateSessionAsync,
            OnChallenge = context =>
            {
                context.HandleResponse();
                var code = context.AuthenticateFailure is SecurityTokenExpiredException ? ErrorCodes.TokenExpired : ErrorCodes.Unauthenticated;
                return ApiProblems.WriteAsync(context.HttpContext, code);
            },
            OnForbidden = context => ApiProblems.WriteAsync(context.HttpContext, ErrorCodes.Forbidden),
        };
    }

    // SEC-50: token hợp lệ về chữ ký nhưng tài khoản đã khóa hoặc đổi security stamp thì bị từ chối
    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirst(AccessTokenClaims.Subject)?.Value;
        var stamp = context.Principal?.FindFirst(AccessTokenClaims.SecurityStamp)?.Value;
        var validator = context.HttpContext.RequestServices.GetRequiredService<AccessTokenSessionValidator>();

        if (!Guid.TryParse(subject, out var userId)
            || !await validator.IsCustomerSessionValidAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.Fail("Phiên đã bị thu hồi.");
        }
    }
}
