using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
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

builder.Services.AddApiControllers();
builder.Services.AddAppHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurityFoundation(builder.Configuration, builder.Environment.IsProduction());
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
    builder.Services.AddOpenApi();
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
