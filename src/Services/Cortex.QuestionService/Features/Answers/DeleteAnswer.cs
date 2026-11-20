using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Answers;

/// <summary>
/// Deletes an answer for good, with its votes. Only its author or an admin may do it, from the
/// current version (<c>?version=</c>, as for questions). Deleting the accepted answer is allowed
/// and clears the question's accepted answer (decided 2026-11-20).
/// </summary>
/// <remarks>
/// Wolverine runs the methods in this order: the FluentValidation middleware (400),
/// <see cref="LoadAsync"/>, <see cref="Validate"/> (404, then 403, then 409), then <see cref="Delete"/>.
/// </remarks>
public static class DeleteAnswerEndpoint
{
    /// <summary>
    /// Loads the answer with its question, both tracked. The answer belongs to the question's
    /// aggregate, so the delete goes through <c>Question.RemoveAnswer</c>, which also clears
    /// <c>AcceptedAnswerId</c> when this was the accepted answer. EF then writes the question first
    /// (<c>accepted_answer_id = NULL</c>, needed because <c>fk_questions_accepted_answer</c> is
    /// RESTRICT) and deletes the answer after; Postgres cascades to its votes.
    /// </summary>
    public static Task<Answer?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Answers
            .Include(answer => answer.Question)
            .FirstOrDefaultAsync(answer => answer.Id == id, ct);

    /// <summary>
    /// <paramref name="query"/> is the one <see cref="Delete"/> binds with <c>[FromQuery]</c>; do not
    /// repeat the attribute here (see "Gotchas already hit" in docs/status.md).
    /// </summary>
    public static IResult Validate(DeleteAnswerQuery query, Answer? answer, CurrentUser user)
    {
        if (answer is null)
            return TypedResults.NotFound();

        if (!answer.CanBeEditedBy(user.Id, user.IsAdmin))
            return Problems.FromError(Answer.Errors.DeleteForbidden);

        if (answer.IsStale(query.Version))
            return Problems.FromError(Concurrency.StaleVersion);

        return WolverineContinue.Result();
    }

    [Authorize]
    [WolverineDelete("/api/answers/{id:guid}")]
    public static async Task<NoContent> Delete(
        [FromQuery] DeleteAnswerQuery query,
        // Without [NotBody], Wolverine would take the loaded answer as the JSON body.
        [NotBody] Answer answer,
        QuestionDbContext db,
        CancellationToken ct)
    {
        // Removing it from the question's collection makes it an orphan of a required
        // relationship, which EF deletes: DELETE ... WHERE answer_id = @id AND xmin = @version.
        answer.Question.RemoveAnswer(answer);

        // Throws DbUpdateConcurrencyException (409) if either row changed after LoadAsync.
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}

/// <summary>The version goes in the query string: a body on DELETE has no defined meaning in HTTP.</summary>
public sealed class DeleteAnswerQuery
{
    public uint Version { get; set; }

    public sealed class DeleteAnswerQueryValidator : AbstractValidator<DeleteAnswerQuery>
    {
        public DeleteAnswerQueryValidator()
        {
            // 0 is what a missing ?version= binds to, and Postgres never uses it as an xmin.
            RuleFor(x => x.Version)
                .NotEmpty()
                .WithMessage("The version is required: send the one you read with the answer.");
        }
    }
}
