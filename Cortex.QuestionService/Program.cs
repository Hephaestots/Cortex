using System.Security.Claims;
using Cortex.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOpenApi();

builder.AddServiceDefaults();

builder.AddCortexAuth();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/me", (ClaimsPrincipal user) => new
{
    Name = user.Identity?.Name,
    Roles = user.FindAll("roles").Select(c => c.Value)
}).RequireAuthorization();

app.MapGet("/admin/ping", () => "ok").RequireAuthorization(Policies.AdminOnly);

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.Run();