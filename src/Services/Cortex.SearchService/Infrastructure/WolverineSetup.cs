using Polly.CircuitBreaker;
using Polly.Timeout;
using Typesense;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;
using Wolverine.RabbitMQ;

namespace Cortex.SearchService.Infrastructure;

public static class WolverineSetup
{
    /// <summary>
    /// Wolverine options for the search service: endpoints, the RabbitMQ queue that receives the
    /// Question service's events, and the error policy for their handlers.
    /// </summary>
    public static WolverineOptions ConfigureSearchService(this WolverineOptions opts)
    {
        // Wolverine 6 does not start without this (see "Gotchas" in docs/status.md).
        opts.UseRuntimeCompilation();
        opts.UseFluentValidation();

        opts.UseRabbitMqUsingNamedConnection("rabbit-mq")
            .AutoProvision();

        // The service's own queue, bound to the Question service's fanout exchange: every service that
        // cares about questions binds its own queue, and each gets its own copy of every event.
        opts.ListenToRabbitQueue("questions.search", config =>
            {
                config.BindExchange("questions");
            })
            // Acknowledge to RabbitMQ only after the handler finished (or the error policy moved the
            // message to the error queue). The default buffers in memory and acknowledges on receipt,
            // so a crash would lose the messages in the buffer. Inline keeps them in RabbitMQ until
            // they're done, and this service has no database for a durable inbox.
            .ProcessInline();

        opts.ConfigureHandlerErrors();

        // AddTypesenseClient registers ITypesenseClient through a factory lambda (a typed HttpClient),
        // which Wolverine's generated code cannot see into, and Wolverine 6 refuses service location
        // by default: the first request to an endpoint taking ITypesenseClient would fail with
        // InvalidServiceLocationException. Allowed for this one type only. Unlike the DbContext in the
        // Question service (where the outbox makes scoping matter), an HTTP client has no per-request
        // state, so resolving it from the container is harmless.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<ITypesenseClient>();

        return opts;
    }

    /// <summary>
    /// What a message handler does when it throws. Rules are matched in this order; a message that
    /// still fails ends in the dead-letter queue (<c>wolverine-dead-letter-queue</c> in RabbitMQ),
    /// where it can be inspected and replayed, never silently dropped.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>Transient</b> (Typesense unreachable, overloaded or slow): retried in place with growing
    /// pauses. Jitter spreads the retries of many failing messages so they don't all hit Typesense at
    /// the same moment. Two layers retry here: each of these four attempts goes through the standard
    /// HTTP resilience handler from ServiceDefaults (its own retries, a 30 s total timeout and a circuit
    /// breaker), so the time before the dead-letter queue is minutes, not the 20 s of pauses alone.</item>
    /// <item><b>Permanent</b> (Typesense rejected the document: bad data, a schema mismatch): retrying
    /// sends the same document again, so it goes to the dead-letter queue at once, to be fixed and replayed.</item>
    /// <item><b>Anything else</b> (most likely a bug in a handler): two quick retries, then the
    /// dead-letter queue.</item>
    /// </list>
    /// Retries in place block this listener during the pauses; fine at this volume. If it ever isn't,
    /// switch the transient rule to <c>ScheduleRetry</c>, which needs durable scheduling.
    /// </remarks>
    private static void ConfigureHandlerErrors(this WolverineOptions opts)
    {
        opts.OnException<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TypesenseApiServiceUnavailableException>()
            // Thrown by the HTTP resilience handler ServiceDefaults puts on every HttpClient, once its own
            // retries are spent (timeout) or while its circuit breaker is open: the same outage, seen
            // through that layer. Without these, an outage would get the "bug" treatment below.
            .Or<TimeoutRejectedException>()
            .Or<BrokenCircuitException>()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(15))
            .WithExponentialJitter()
            .Then.MoveToErrorQueue();

        opts.OnException<TypesenseApiBadRequestException>()
            .Or<TypesenseApiUnprocessableEntityException>()
            .MoveToErrorQueue();

        opts.OnAnyException()
            .RetryTimes(2)
            .Then.MoveToErrorQueue();
    }
}
