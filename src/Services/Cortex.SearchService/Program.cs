using Cortex.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: service discovery (resolves the typesense endpoint), resilience, telemetry
// and the health checks behind /health, which the AppHost's WithHttpHealthCheck waits on.
builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.Run();
