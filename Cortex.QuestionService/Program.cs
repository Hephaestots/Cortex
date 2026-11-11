using System.Security.Claims;
using Cortex.ServiceDefaults;
using Wolverine;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UseWolverine(opts =>
{
    opts.UseRuntimeCompilation();
});

builder.Services.AddOpenApi();
builder.Services.AddWolverineHttp();

builder.AddServiceDefaults();

builder.AddCortexAuth();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints();

app.Run();