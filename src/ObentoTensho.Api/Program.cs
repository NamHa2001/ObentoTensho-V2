using ObentoTensho.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSecurityFoundation(builder.Configuration);

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health/live");

app.Run();
