using Typesense;
using Wolverine;
using Wolverine.FluentValidation;

namespace Cortex.SearchService.Infrastructure;

public static class WolverineSetup
{
    /// <summary>
    /// Wolverine options for the search service. Today: endpoints only. The RabbitMQ listeners for
    /// the question events come here next.
    /// </summary>
    public static WolverineOptions ConfigureSearchService(this WolverineOptions opts)
    {
        // Wolverine 6 does not start without this (see "Gotchas" in docs/status.md).
        opts.UseRuntimeCompilation();
        opts.UseFluentValidation();

        // AddTypesenseClient registers ITypesenseClient through a factory lambda (a typed HttpClient),
        // which Wolverine's generated code cannot see into, and Wolverine 6 refuses service location
        // by default: the first request to an endpoint taking ITypesenseClient would fail with
        // InvalidServiceLocationException. Allowed for this one type only. Unlike the DbContext in the
        // Question service (where the outbox makes scoping matter), an HTTP client has no per-request
        // state, so resolving it from the container is harmless.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITypesenseClient>();

        return opts;
    }
}
