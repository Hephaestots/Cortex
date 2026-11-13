using Cortex.QuestionService.Data;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="QuestionDbContext"/> through Aspire's Npgsql integration, which
    /// also adds a Postgres health check, retries and telemetry.
    /// </summary>
    /// <remarks>
    /// Extends the builder rather than IServiceCollection because the Aspire integration
    /// needs the configuration and the health check / telemetry builders.
    /// </remarks>
    public static IHostApplicationBuilder AddQuestionServiceData(this IHostApplicationBuilder builder)
    {
        // "questions-db" is the AppHost resource name, exposed as ConnectionStrings:questions-db.
        // Snake case covers every name EF generates itself (keys, indexes, join tables), so
        // nothing needs quoting in Postgres. Explicit HasColumnName / HasConstraintName still win.
        builder.AddNpgsqlDbContext<QuestionDbContext>("questions-db",
            configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());

        return builder;
    }

    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations. Call it in Development only; other environments
    /// run migrations as a deploy step. Under the AppHost, WaitFor(questionsDb) makes sure
    /// Postgres is up first. A failure stops the service from starting, which is intended.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
        await db.Database.MigrateAsync();
    }
}
