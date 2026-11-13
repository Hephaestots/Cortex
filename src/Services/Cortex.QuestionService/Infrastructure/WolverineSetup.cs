using Wolverine;

namespace Cortex.QuestionService.Infrastructure;

public static class WolverineSetup
{
    /// <summary>
    /// Wolverine options for the question service. Grows in the stages listed under
    /// "Program.cs" in docs/architecture.md; today it is stage 1 (endpoints only).
    /// </summary>
    public static WolverineOptions ConfigureQuestionService(this WolverineOptions opts)
    {
        // Wolverine 6 does not start without this (see "Gotchas" in docs/status.md).
        opts.UseRuntimeCompilation();

        // Build step 4 adds here: EF Core transactions, the Postgres outbox and RabbitMQ routing.
        return opts;
    }
}
