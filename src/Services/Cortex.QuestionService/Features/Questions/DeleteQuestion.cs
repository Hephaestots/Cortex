using Cortex.Contracts.Features.Questions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Questions;

/// <summary>
/// Deletes a question for good, with its answers, votes and tag links. Only the author or an
/// admin may do it, and only from the current version: <c>?version=</c> is the one the client read.
/// </summary>
/// <remarks>
/// Wolverine runs the methods in this order: the FluentValidation middleware (400),
/// <see cref="LoadAsync"/>, <see cref="Validate"/> (404, then 403, then 409), then <see cref="Delete"/>.
/// </remarks>
public static class DeleteQuestionEndpoint
{
    /// <summary>
    /// Loads only the question row, tracked (for its version), and on purpose without answers:
    /// Postgres cascades the delete to answers, votes and the <c>question_tags</c> links itself
    /// (the tags stay). If EF had the answers loaded it would delete them first, while the
    /// question still references the accepted one (<c>fk_questions_accepted_answer</c> is RESTRICT).
    /// </summary>
    public static Task<Question?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Questions.FirstOrDefaultAsync(question => question.Id == id, ct);

    /// <summary>
    /// <paramref name="query"/> is the one <see cref="Delete"/> binds with <c>[FromQuery]</c>; Wolverine
    /// passes the same variable here. Do not repeat <c>[FromQuery]</c> on this method: Wolverine then
    /// generates the binding twice and the endpoint fails to compile at runtime (500).
    /// </summary>
    public static IResult Validate(DeleteQuestionQuery query, Question? question, CurrentUser user)
    {
        if (question is null)
            return TypedResults.NotFound();

        if (!question.CanBeEditedBy(user.Id, user.IsAdmin))
            return Problems.FromError(Question.Errors.DeleteForbidden);

        if (question.IsStale(query.Version))
            return Problems.FromError(Concurrency.StaleVersion);

        return WolverineContinue.Result();
    }

    [Authorize]
    [WolverineDelete("/api/questions/{id:guid}")]
    public static async Task<(NoContent, QuestionDeleted?)> Delete(
        // Not used in the body: bound here because [FromQuery] only works on the endpoint method,
        // and that is also what makes the FluentValidation middleware validate it.
        [FromQuery] DeleteQuestionQuery query,
        // Without [NotBody], Wolverine takes the first complex parameter of an endpoint that has
        // no JSON request type as the body, and every call is a 400 "Invalid JSON format".
        [NotBody] Question question,
        QuestionDbContext db,
        CancellationToken ct)
    {
        db.Questions.Remove(question);

        // DELETE ... WHERE question_id = @id AND xmin = @version: throws
        // DbUpdateConcurrencyException (409) if the row changed after LoadAsync.
        await db.SaveChangesAsync(ct);

        return (
            TypedResults.NoContent(),
            question.ToQuestionDeleted());
    }
}

/// <summary>
/// The version goes in the query string, not a body: a body on DELETE has no defined meaning
/// in HTTP, and some clients and proxies drop it.
/// </summary>
public sealed class DeleteQuestionQuery
{
    public uint Version { get; set; }

    public sealed class DeleteQuestionQueryValidator : AbstractValidator<DeleteQuestionQuery>
    {
        public DeleteQuestionQueryValidator()
        {
            // 0 is what a missing ?version= binds to, and Postgres never uses it as an xmin.
            RuleFor(x => x.Version)
                .NotEmpty()
                .WithMessage("The version is required: send the one you read with the question.");
        }
    }
}
