using Cortex.SearchService.Infrastructure;
using Cortex.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTypesense();
builder.Services.AddSearchCollections();
builder.Services.AddOpenApi();

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

app.Run();
