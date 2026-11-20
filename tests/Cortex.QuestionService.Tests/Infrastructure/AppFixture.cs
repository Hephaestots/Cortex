using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Cortex.QuestionService.Tests.Infrastructure.AppFixture))]

namespace Cortex.QuestionService.Tests.Infrastructure;

/// <summary>
/// One Postgres container and one in-memory question service for the whole test run.
/// Test classes take it as a constructor parameter (an xUnit v3 assembly fixture).
/// </summary>
/// <remarks>
/// <para>
/// The host is the real <c>Program.cs</c> pipeline. The only swaps are the connection string
/// (pointing at the container) and the authentication scheme (<see cref="TestAuthHandler"/>).
/// </para>
/// <para>
/// It runs as Development, so startup applies the migrations and seeds the tags exactly as
/// under the AppHost. Tests share the database: each test creates the data it needs and must
/// not assume it is alone (no fixed counts of questions, say). Seeded tags are the exception.
/// </para>
/// </remarks>
public sealed class AppFixture : IAsyncLifetime
{
    // The same major version the AppHost runs, so Postgres behaves the same in tests.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("questions-db")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Development");

            // The name the AppHost gives the database resource; see AddQuestionServiceData.
            builder.UseSetting("ConnectionStrings:questions-db", _postgres.GetConnectionString());

            // Runs after Program.cs registered its services, so these defaults win over the
            // JWT bearer scheme that AddCortexAuth set. Keycloak is never contacted.
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { }));
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
            await Host.DisposeAsync();

        await _postgres.DisposeAsync();
    }
}
