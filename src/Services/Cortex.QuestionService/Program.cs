using Cortex.QuestionService.Infrastructure;
using Cortex.ServiceDefaults;
using Wolverine;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddCortexAuth();
builder.AddQuestionServiceData();
builder.Services.AddCurrentUser();

builder.Host.UseWolverine(opts => opts.ConfigureQuestionService());
builder.Services.AddWolverineHttp();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapWolverineEndpoints();

app.Run();
