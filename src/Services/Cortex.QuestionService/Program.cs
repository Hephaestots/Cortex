using Cortex.QuestionService.Infrastructure;
using Cortex.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddCortexAuth();
builder.AddQuestionServiceData();
builder.Services.AddCurrentUser();
builder.Services.AddErrorHandling();
builder.Services.TryAddSingleton(TimeProvider.System);

builder.Host.UseWolverine(opts => opts.ConfigureQuestionService());
builder.Services.AddWolverineHttp();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyMigrationsAsync();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
    opts.RejectUnparseableQueryValues = true;
});

app.Run();
