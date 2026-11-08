using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Core.Audit.Services;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Configuration;

public static class AuthConfiguration
{
    public const string AdminCorsPolicy = "admin";

    private static readonly string[] AllowedHeaders =
        ["Authorization", "Content-Type", StepUpFilter.Header, TrustedOriginFilter.ClientHeader, TurnstileFilter.Header, "Idempotency-Key", "If-Match"];

    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IValidator<StaffChangePasswordRequest>, StaffChangePasswordRequestValidator>();
        services.AddSingleton<IValidator<AcceptInviteRequest>, AcceptInviteRequestValidator>();
        services.AddSingleton<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();

        services.AddScoped<SessionFactory>();
        services.AddScoped<LoginGuard>();
        services.AddScoped<AccessTokenSessionValidator>();
        services.AddScoped<CustomerSessionService>();
        services.AddScoped<CustomerRegistrationService>();
        services.AddScoped<CustomerAccountService>();
        services.AddScoped<StaffPasswordResetService>();
        services.AddScoped<StaffSignInFlow>();
        services.AddScoped<StaffLoginService>();
        services.AddScoped<StaffTwoFactorService>();
        services.AddScoped<StaffPasswordService>();
        services.AddScoped<StaffSessionService>();
        services.AddScoped<StaffBootstrapService>();
        services.AddScoped<StaffAdminService>();
        services.AddScoped<StaffStepUpService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<StepUpFilter>();
        services.AddSingleton<IValidator<CreateStaffRequest>, CreateStaffRequestValidator>();
        services.AddSingleton<IValidator<UpdateStaffRequest>, UpdateStaffRequestValidator>();
        services.AddSingleton<IValidator<ChangeOwnPasswordRequest>, ChangeOwnPasswordRequestValidator>();
        services.AddScoped<CustomerOriginFilter>();
        services.AddScoped<AdminOriginFilter>();

        services.AddAuthentication(AuthSchemes.Customer).AddJwtBearer(AuthSchemes.Customer).AddJwtBearer(AuthSchemes.Staff);
        services.AddOptions<JwtBearerOptions>(AuthSchemes.Customer)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => ConfigureBearer(bearer, jwt.Value, jwt.Value.CustomerAudience, AuthSchemes.Customer));
        services.AddOptions<JwtBearerOptions>(AuthSchemes.Staff)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => ConfigureBearer(bearer, jwt.Value, jwt.Value.AdminAudience, AuthSchemes.Staff));

        services.AddAuthorizationBuilder().AddAppPolicies();
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContext, HttpAuditContext>();
        services.AddScoped<AuthSecurityEvents>();

        return services;
    }

    public static IServiceCollection AddWebOrigins(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebOriginsOptions>()
            .Bind(configuration.GetSection(WebOriginsOptions.SectionName))
            .Validate(o => o.Validate() is null, "Cấu hình Web:CustomerOrigins / Web:AdminOrigins không hợp lệ.")
            .ValidateOnStart();

        // SEC-41: CORS theo bề mặt — mặc định chỉ origin khách; API quản trị gắn [EnableCors(AdminCorsPolicy)]
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<WebOriginsOptions>>((cors, web) =>
        {
            cors.AddDefaultPolicy(policy => ConfigurePolicy(policy, web.Value.CustomerOrigins));
            cors.AddPolicy(AdminCorsPolicy, policy => ConfigurePolicy(policy, web.Value.AdminOrigins));
        });

        return services;
    }

    private static void ConfigurePolicy(CorsPolicyBuilder policy, IEnumerable<string> origins) =>
        policy.WithOrigins([.. origins])
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders(AllowedHeaders)
            .AllowCredentials();

    private static void ConfigureBearer(JwtBearerOptions bearer, JwtOptions jwt, string audience, string scheme)
    {
        var isStaff = string.Equals(scheme, AuthSchemes.Staff, StringComparison.Ordinal);
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = audience,
            IssuerSigningKeys = jwt.GetValidationKeys().ToList(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = JwtOptions.ClockSkew,
            NameClaimType = AccessTokenClaims.Subject,
            RoleClaimType = AccessTokenClaims.Role,

            // Audit phân biệt người gọi là nhân viên hay khách theo loại xác thực
            AuthenticationType = scheme,
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => ValidateSessionAsync(context, isStaff),
            // Endpoint nhận cả hai loại token ⇒ cả hai scheme cùng challenge; scheme sau không ghi đè phản hồi đã gửi
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return Task.CompletedTask;
                }

                var code = context.AuthenticateFailure is SecurityTokenExpiredException ? ErrorCodes.TokenExpired : ErrorCodes.Unauthenticated;
                return ApiProblems.WriteAsync(context.HttpContext, code);
            },
            OnForbidden = context => context.Response.HasStarted ? Task.CompletedTask : ApiProblems.WriteAsync(context.HttpContext, ErrorCodes.Forbidden),
        };
    }

    // SEC-50: token hợp lệ về chữ ký nhưng tài khoản đã khóa hoặc đổi security stamp thì bị từ chối
    private static async Task ValidateSessionAsync(TokenValidatedContext context, bool isStaff)
    {
        var subject = context.Principal?.FindFirst(AccessTokenClaims.Subject)?.Value;
        var stamp = context.Principal?.FindFirst(AccessTokenClaims.SecurityStamp)?.Value;
        var validator = context.HttpContext.RequestServices.GetRequiredService<AccessTokenSessionValidator>();
        var cancellationToken = context.HttpContext.RequestAborted;

        var valid = Guid.TryParse(subject, out var id) && (isStaff
            ? await validator.IsStaffSessionValidAsync(id, stamp, cancellationToken)
            : await validator.IsCustomerSessionValidAsync(id, stamp, cancellationToken));

        // SEC-50: phiên đã đăng xuất/bị thu hồi ⇒ access token còn hạn cũng không dùng được
        if (valid && SessionOf(context) is { } sessionId && context.HttpContext.RequestServices.GetRequiredService<ISessionDenylist>().IsRevoked(sessionId))
        {
            valid = false;
        }

        if (!valid)
        {
            context.Fail("Phiên đã bị thu hồi.");
        }
    }

    private static Guid? SessionOf(TokenValidatedContext context) =>
        Guid.TryParse(context.Principal?.FindFirst(AccessTokenClaims.SessionId)?.Value, out var sid) ? sid : null;
}
