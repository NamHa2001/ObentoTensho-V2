using ObentoTensho.Api.Configuration;
using Microsoft.Extensions.Logging.Console;
using ObentoTensho.Api.Http;
using ObentoTensho.Api.Logging;
using ObentoTensho.Api.Realtime;
using ObentoTensho.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    // docs/srs/09 §7: không trả header Server; body mặc định tối đa 1 MB
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1024 * 1024;
});

// SEC-53: log JSON một dòng, có traceId, đã che dữ liệu nhạy cảm
builder.Logging.ClearProviders();
builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
builder.Logging.AddConsole(o => o.FormatterName = RedactingJsonConsoleFormatter.FormatterName)
    .AddConsoleFormatter<RedactingJsonConsoleFormatter, ConsoleFormatterOptions>();

builder.Services.AddApiControllers();
builder.Services.AddAppHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurityFoundation(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddSingleton(new ObentoTensho.Core.Auth.Domain.StaffTwoFactorPolicy(
    builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Security:SkipStaffTwoFactor")));
builder.Services.AddMessaging(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddWebOrigins(builder.Configuration);
builder.Services.AddForwardedHeadersFromConfig(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddAuthServices();
builder.Services.AddTablesServices();
builder.Services.AddReservationServices();
builder.Services.AddMenuServices();
builder.Services.AddOrderServices();
builder.Services.AddLoyaltyServices();
builder.Services.AddVoucherServices();
builder.Services.AddContentServices();
builder.Services.AddCustomerServices();
builder.Services.AddRealtime();
if (builder.Environment.IsDevelopment())
{
    // Chỉ để OpenAPI mô tả đúng dữ liệu thật: enum là chuỗi snake_case, số là số
    builder.Services.ConfigureHttpJsonOptions(o =>
    {
        o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
        o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    });
    builder.Services.AddOpenApi(o => o.AddDocumentTransformer<ContractSchemasTransformer>());
}

var app = builder.Build();

// SEC-02: IP thật phải có trước giới hạn tần suất và audit
app.UseForwardedHeaders();
app.UseApiExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Configuration.GetValue("Media:ServeFromApi", app.Environment.IsDevelopment()))
{
    app.UseMediaFiles();
}

app.UseRouting();
app.UseMiddleware<HubOriginMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRealtimeHubs();
app.MapAppHealthChecks();

// Trang tài liệu + thử API chỉ có ở máy dev, không bao giờ mở ở môi trường chạy thật
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

await app.SeedSuperAdminAsync();
await app.RunAsync();
