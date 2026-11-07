using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    // docs/srs/09 §7: không trả header Server; body mặc định tối đa 1 MB
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1024 * 1024;
});

builder.Services.AddApiControllers();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurityFoundation(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddWebOrigins(builder.Configuration);
builder.Services.AddForwardedHeadersFromConfig(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddAuthServices();
builder.Services.AddTablesServices();
builder.Services.AddReservationServices();
builder.Services.AddMenuServices();

var app = builder.Build();

// SEC-02: IP thật phải có trước giới hạn tần suất và audit
app.UseForwardedHeaders();
app.UseApiExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();

await app.SeedSuperAdminAsync();
await app.RunAsync();
