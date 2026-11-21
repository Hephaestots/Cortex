using Cortex.QuestionService.Data;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Cortex.QuestionService.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="QuestionDbContext"/> with Wolverine's EF Core integration, then lets
    /// Aspire enrich it with a Postgres health check, retries and telemetry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not Aspire's <c>AddNpgsqlDbContext</c>: it registers the context through a factory lambda,
    /// which Wolverine's generated endpoint code cannot see into, and Wolverine 6 rejects the
    /// service location that would need. See "Gotchas already hit" in docs/status.md.
    /// <c>AddDbContextWithWolverineIntegration</c> (build step 4) registers it by type, as before,
    /// and also maps Wolverine's outbox tables into the context, so the transactional middleware
    /// saves the entities and the outgoing messages in one <c>SaveChanges</c>.
    /// </para>
    /// <para>
    /// Extends the builder rather than IServiceCollection because it needs the configuration,
    /// and the Aspire enrichment needs the health check / telemetry builders.
    /// </para>
    /// </remarks>
    public static IHostApplicationBuilder AddQuestionServiceData(this IHostApplicationBuilder builder)
    {
        // "questions-db" is the AppHost resource name, exposed as ConnectionStrings:questions-db.
        // Snake case covers every name EF generates itself (keys, indexes, join tables), so
        // nothing needs quoting in Postgres. Explicit HasColumnName / HasConstraintName still win.
        builder.Services.AddDbContextWithWolverineIntegration<QuestionDbContext>(options => options
                .UseNpgsql(builder.Configuration.GetConnectionString("questions-db"))
                .UseSnakeCaseNamingConvention()
                .UseSeeding((context, _) => TagSeeder.Seed(context))
                .UseAsyncSeeding((context, _, ct) => TagSeeder.SeedAsync(context, ct)),
            wolverineDatabaseSchema: "wolverine");

        // DisableRetry: Aspire turns on EF's retry-on-failure by default, and EF's retrying execution
        // strategy forbids the explicit transaction Wolverine's Eager middleware opens (Wolverine refuses
        // to start with both). Eager wins because it keeps CreateQuestion's own save and the outbox row
        // in one commit. Cost: a transient database error during a request is not retried by EF; the
        // request fails and the client retries it. Messages keep Wolverine's own retry policies.
        builder.EnrichNpgsqlDbContext<QuestionDbContext>(settings => settings.DisableRetry = true);
        
        return builder;
    }

    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyExceptionHandler>();

        return services;
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
