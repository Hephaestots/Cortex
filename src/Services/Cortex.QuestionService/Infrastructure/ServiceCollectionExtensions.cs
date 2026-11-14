using Cortex.QuestionService.Data;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="QuestionDbContext"/> with plain <c>AddDbContext</c>, then lets Aspire
    /// enrich it with a Postgres health check, retries and telemetry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not Aspire's <c>AddNpgsqlDbContext</c>: it registers the context through a factory lambda,
    /// which Wolverine's generated endpoint code cannot see into, and Wolverine 6 rejects the
    /// service location that would need. Registered by type with singleton options, Wolverine
    /// constructs the context inline. See "Gotchas already hit" in docs/status.md.
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
        builder.Services.AddDbContext<QuestionDbContext>(options => options
                .UseNpgsql(builder.Configuration.GetConnectionString("questions-db"))
                .UseSnakeCaseNamingConvention()
                .UseSeeding((context, _) => TagSeeder.Seed(context))
                .UseAsyncSeeding((context, _, ct) => TagSeeder.SeedAsync(context, ct)),
            optionsLifetime: ServiceLifetime.Singleton);

        builder.EnrichNpgsqlDbContext<QuestionDbContext>();
        
        return builder;
    }

    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();

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
