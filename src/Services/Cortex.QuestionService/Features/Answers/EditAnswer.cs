using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Answers;

/// <summary>
/// Replaces an answer's body. Only its author or an admin may do it, from the current version.
/// </summary>
/// <remarks>
/// Wolverine runs the methods in this order: the FluentValidation middleware (400),
/// <see cref="LoadAsync"/>, <see cref="Validate"/> (404, then 403, then 409), then <see cref="Put"/>.
/// The same shape as <c>EditQuestion</c>.
/// </remarks>
public static class EditAnswerEndpoint
{
    /// <summary>
    /// Loads the answer row only, tracked, so EF keeps the loaded <c>Version</c> for the
    /// UPDATE's <c>AND xmin = @version</c>. Nothing about the question changes.
    /// </summary>
    public static Task<Answer?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Answers.FirstOrDefaultAsync(answer => answer.Id == id, ct);

    public static IResult Validate(EditAnswerRequest request, Answer? answer, CurrentUser user)
    {
        if (answer is null)
            return TypedResults.NotFound();

        if (!answer.CanBeEditedBy(user.Id, user.IsAdmin))
            return Problems.FromError(Answer.Errors.EditForbidden);

        if (answer.IsStale(request.Version))
            return Problems.FromError(Concurrency.StaleVersion);

        return WolverineContinue.Result();
    }

    [Authorize]
    [WolverinePut("/api/answers/{id:guid}")]
    public static async Task<NoContent> Put(
        EditAnswerRequest request,
        Answer answer,
        QuestionDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        answer.Edit(request.Body, timeProvider.GetUtcNow());

        // Throws DbUpdateConcurrencyException (409) if the row changed after LoadAsync.
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}

/// <summary>
/// The body is the only editable field. <see cref="Version"/> is the one the client read
/// (<c>GetAnswerById</c>, <c>GetAnswers</c> or <c>CreateAnswer</c>).
/// </summary>
public sealed record EditAnswerRequest(string Body, uint Version)
{
    public sealed class EditAnswerValidator : AbstractValidator<EditAnswerRequest>
    {
        public EditAnswerValidator()
        {
            // 0 is what a missing "version" binds to, and Postgres never uses it as an xmin.
            RuleFor(x => x.Version)
                .NotEmpty()
                .WithMessage("The version is required: send the one you read with the answer.");

            RuleFor(x => x.Body)
                .NotEmpty()
                .WithError(Answer.Errors.EmptyBody)
                .MaximumLength(Answer.MaxBodyLength)
                .WithError(Answer.Errors.BodyTooLong);
        }
    }
}
