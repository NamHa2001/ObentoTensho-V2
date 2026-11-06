using ObentoTensho.Api.Configuration;
using ObentoTensho.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// docs/srs/09 §7: không trả header Server
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddApiControllers();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurityFoundation(builder.Configuration);
builder.Services.AddWebOrigins(builder.Configuration);
builder.Services.AddCustomerAuth();

var app = builder.Build();

app.UseApiExceptionHandler();
app.UseCors(AuthConfiguration.CustomerCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live").AllowAnonymous();

app.Run();
