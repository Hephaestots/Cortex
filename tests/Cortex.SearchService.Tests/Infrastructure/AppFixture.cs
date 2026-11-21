using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Wolverine;

[assembly: AssemblyFixture(typeof(Cortex.SearchService.Tests.Infrastructure.AppFixture))]

namespace Cortex.SearchService.Tests.Infrastructure;

/// <summary>
/// One Typesense container and one in-memory search service for the whole test run.
/// Test classes take it as a constructor parameter (an xUnit v3 assembly fixture).
/// </summary>
/// <remarks>
/// <para>
/// Typesense has no Testcontainers module (unlike <c>PostgreSqlBuilder</c> in the Question tests),
/// so the container is described with the generic <see cref="ContainerBuilder"/>: image, settings,
/// ports and how to tell it is ready. A module is only a preset of these same calls.
/// </para>
/// <para>
/// The host is the real <c>Program.cs</c> pipeline, so startup creates the collections exactly as
/// under the AppHost. The only swaps are the Typesense endpoint and key (pointing at the
/// container) and RabbitMQ (disabled). Tests share the collection: each one uses ids and tags
/// only it knows and must not assume it is alone.
/// </para>
/// </remarks>
public sealed class AppFixture : IAsyncLifetime
{
    // Typesense's container port; the host port is random (see WithPortBinding below).
    private const int TypesensePort = 8108;

    // Only this container ever sees it, so a constant in the source is fine (the AppHost's is a secret).
    private const string ApiKey = "test-api-key";

    private readonly IContainer _typesense = new ContainerBuilder("typesense/typesense:30.2")
        // The same image as the AppHost. Typesense reads TYPESENSE_* variables as well as --flags.
        .WithEnvironment("TYPESENSE_API_KEY", ApiKey)
        .WithCommand("--data-dir", "/data")
        // In memory: nothing to clean up, and no volume shared with the AppHost's typesense-data.
        .WithTmpfsMount("/data")
        // true = Docker picks a free host port, so tests never clash with the AppHost's 8108 or
        // with another test run. GetMappedPublicPort reads it back once the container runs.
        .WithPortBinding(TypesensePort, true)
        // StartAsync returns only when this succeeds. Without it, the container counts as started
        // as soon as the process runs, and the host's startup (which creates the collections)
        // races Typesense's own startup. Tried without it: still green, because ServiceDefaults'
        // HTTP resilience handler retries the refused connections, which hides the race rather
        // than removing it. /health answers 200 {"ok":true} once Typesense can serve, the same
        // check the AppHost's WithHttpHealthCheck uses.
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request
                .ForPort(TypesensePort)
                .ForPath("/health")))
        .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _typesense.StartAsync();

        var endpoint = new UriBuilder(
            Uri.UriSchemeHttp,
            _typesense.Hostname,
            _typesense.GetMappedPublicPort(TypesensePort)).Uri;

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Development");

            // What the AppHost passes through service discovery and the environment; see AddTypesense.
            builder.UseSetting("services:typesense:http:0", endpoint.ToString());
            builder.UseSetting("Typesense:ApiKey", ApiKey);

            // Never contacted (see DisableAllExternalWolverineTransports below), but Wolverine checks at
            // startup that the RabbitMQ connection string it was configured with exists.
            builder.UseSetting("ConnectionStrings:rabbit-mq", "amqp://guest:guest@localhost:5672");

            // No broker in tests: the questions.search listener never starts, and tests hand events to
            // the handlers through Wolverine directly.
            builder.ConfigureTestServices(services => services.DisableAllExternalWolverineTransports());
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
            await Host.DisposeAsync();

        await _typesense.DisposeAsync();
    }
}
