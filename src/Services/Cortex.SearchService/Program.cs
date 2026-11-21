using Cortex.SearchService.Infrastructure;
using Cortex.ServiceDefaults;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTypesense();
builder.Services.AddSearchCollections();
builder.Services.AddSearchOpenTelemetry(builder);
builder.Services.AddOpenApi();

builder.Host.UseWolverine(opts => opts.ConfigureSearchService());
builder.Services.AddWolverineHttp();

var app = builder.Build();

// Before app.Run(), so the collections exist before any message is handled. A timeout instead of
// a shutdown token: Ctrl+C handling only starts with app.Run(), so it could not cancel this.
using (var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
{
    await app.Services
        .GetRequiredService<SearchCollectionsInitializer>()
        .EnsureCollectionsExistAsync(startupTimeout.Token);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

// The same options as the Question service: validation failures as problem details, and
// ?page=abc as a 400 instead of silently using the default value.
app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
    opts.RejectUnparseableQueryValues = true;
});

app.Run();
