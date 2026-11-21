using Cortex.Contracts.Features.Questions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Shared;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Questions;

/// <summary>
/// Replaces a question's title, body and tags. Only the author or an admin may do it.
/// The book and the author never change, so the request does not carry them.
/// </summary>
/// <remarks>
/// Wolverine runs the methods in this order: the FluentValidation middleware (400),
/// <see cref="LoadAsync"/>, <see cref="Validate"/> (404, then 403, then 409), then <see cref="Put"/>.
/// </remarks>
public static class EditQuestionEndpoint
{
    /// <summary>
    /// Loads the question, tracked and with its tags: <c>Question.Edit</c> replaces the tag list,
    /// and EF can only work out which join rows to add or delete if the current ones are loaded.
    /// Tracking also keeps the loaded <c>Version</c>, which EF puts in the UPDATE's WHERE clause.
    /// </summary>
    public static Task<Question?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Questions
            .Include(question => question.Tags)
            .FirstOrDefaultAsync(question => question.Id == id, ct);

    /// <summary>
    /// Returning anything but <c>WolverineContinue.Result()</c> stops the request with that result.
    /// The 404 is checked here rather than with <c>[Required]</c> so the order of the checks is
    /// visible in one place.
    /// </summary>
    public static IResult Validate(EditQuestionRequest request, Question? question, CurrentUser user)
    {
        if (question is null)
            return TypedResults.NotFound();

        if (!question.CanBeEditedBy(user.Id, user.IsAdmin))
            return Problems.FromError(Question.Errors.EditForbidden);

        if (question.IsStale(request.Version))
            return Problems.FromError(Concurrency.StaleVersion);

        return WolverineContinue.Result();
    }

    [Authorize]
    [WolverinePut("/api/questions/{id:guid}")]
    public static async Task<(Results<NoContent, ValidationProblem>, QuestionUpdated?)> Put(
        EditQuestionRequest request,
        Question question,
        QuestionDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var lookup = await TagLookup.FindBySlugsAsync(db, request.TagSlugs, ct);

        if (lookup.HasMissing)
            return (
                Problems.Validation(nameof(EditQuestionRequest.TagSlugs), lookup.MissingMessages),
                null);

        question.Edit(request.Title, request.Body, lookup.Tags, timeProvider.GetUtcNow());

        // Throws DbUpdateConcurrencyException (409) if the row changed after LoadAsync.
        await db.SaveChangesAsync(ct);

        return (
            TypedResults.NoContent(),
            question.ToQuestionUpdated());
    }
}

/// <summary>
/// A full replacement of the editable fields (PUT), so all three are required.
/// <see cref="Version"/> is the one the client read (<c>GetQuestionById</c> or
/// <c>CreateQuestion</c>); an edit based on an older version is a 409.
/// </summary>
public sealed record EditQuestionRequest(
    string Title,
    string Body,
    IReadOnlyList<string> TagSlugs,
    uint Version)
{
    public sealed class EditQuestionValidator : AbstractValidator<EditQuestionRequest>
    {
        public EditQuestionValidator()
        {
            // 0 is what a missing "version" binds to, and Postgres never uses it as an xmin.
            RuleFor(x => x.Version)
                .NotEmpty()
                .WithMessage("The version is required: send the one you read with the question.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithError(Question.Errors.EmptyTitle)
                .MaximumLength(Question.MaxTitleLength)
                .WithError(Question.Errors.TitleTooLong);

            RuleFor(x => x.Body)
                .NotEmpty()
                .WithError(Question.Errors.EmptyBody)
                .MaximumLength(Question.MaxBodyLength)
                .WithError(Question.Errors.BodyTooLong);

            RuleFor(x => x.TagSlugs)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(s => s is { Count: >= 1 and <= Question.MaxTags })
                .WithError(Question.Errors.TagsOutOfRange);

            RuleForEach(x => x.TagSlugs)
                .NotEmpty()
                .WithError(Tag.Errors.EmptySlug)
                .MaximumLength(Tag.MaxSlugLength)
                .WithError(Tag.Errors.SlugTooLong);
        }
    }
}
