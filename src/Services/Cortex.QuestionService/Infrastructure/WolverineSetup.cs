using Cortex.Contracts;
using Cortex.Contracts.Features.Questions;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.Persistence;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Cortex.QuestionService.Infrastructure;

public static class WolverineSetup
{
    /// <summary>
    /// Wolverine options for the question service: endpoints, plus (build step 4) the Postgres outbox
    /// and the RabbitMQ routing of the integration events.
    /// </summary>
    /// <param name="opts">The options to configure.</param>
    /// <param name="configuration">For the connection string of the outbox tables.</param>
    public static WolverineOptions ConfigureQuestionService(this WolverineOptions opts, IConfiguration configuration)
    {
        // Wolverine 6 does not start without this (see "Gotchas" in docs/status.md).
        opts.UseRuntimeCompilation();
        opts.UseFluentValidation();

        // The outbox: an event an endpoint returns is written to these tables in the same transaction
        // as the entity changes, then sent to RabbitMQ by a background agent that retries until it
        // succeeds. Without it, a save could succeed while the event is lost (the dual-write problem).
        // Its own schema in the service's database; a plain name, since Postgres needs quotes around a hyphen.
        opts.PersistMessagesWithPostgresql(
            configuration.GetConnectionString("questions-db")
                ?? throw new InvalidOperationException("No connection string 'questions-db'."),
            "wolverine");

        // Eager: the transaction is opened before the endpoint runs, so an endpoint may call
        // SaveChangesAsync itself (CreateQuestion does, to read back the question's version for its
        // response) and that save and the outbox row still commit together.
        opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Eager);
        opts.Policies.AutoApplyTransactions();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

        opts.UseRabbitMqUsingNamedConnection("rabbit-mq")
            .AutoProvision();

        // Each integration event is routed explicitly, so a message meant to stay inside the service
        // (a future QuestionViewed, say) never reaches the exchange by accident.
        opts.PublishMessage<QuestionCreated>().ToRabbitExchange("questions");
        opts.PublishMessage<QuestionUpdated>().ToRabbitExchange("questions");
        opts.PublishMessage<QuestionDeleted>().ToRabbitExchange("questions");

        return opts;
    }
}
