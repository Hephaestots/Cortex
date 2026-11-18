using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
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
/// <see cref="LoadAsync"/>, <see cref="Validate"/> (404, then 403), then <see cref="Put"/>.
/// </remarks>
public static class EditQuestionEndpoint
{
    /// <summary>
    /// Loads the question, tracked and with its tags: <c>Question.Edit</c> replaces the tag list,
    /// and EF can only work out which join rows to add or delete if the current ones are loaded.
    /// </summary>
    public static Task<Question?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Questions
            .Include(question => question.Tags)
            .FirstOrDefaultAsync(question => question.Id == id, ct);

    /// <summary>
    /// Returning anything but <c>WolverineContinue.Result()</c> stops the request with that result.
    /// The 404 is checked here rather than with <c>[Required]</c> so the order of the two checks is
    /// visible in one place.
    /// </summary>
    public static IResult Validate(Question? question, CurrentUser user)
    {
        if (question is null)
            return TypedResults.NotFound();

        if (!question.CanBeEditedBy(user.Id, user.IsAdmin))
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: Question.Errors.EditForbidden.Message,
                extensions: new Dictionary<string, object?> { ["code"] = Question.Errors.EditForbidden.Code });

        return WolverineContinue.Result();
    }

    [Authorize]
    [WolverinePut("/api/questions/{id:guid}")]
    public static async Task<Results<NoContent, ValidationProblem>> Put(
        EditQuestionRequest request,
        Question question,
        QuestionDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        // Same tag resolution as CreateQuestion: normalise, load, reject the unknown ones.
        var normalizedSlugs = request.TagSlugs
            .Select(Tag.Normalize)
            .ToHashSet();

        var tags = await db.Tags.Where(tag => normalizedSlugs.Contains(tag.Slug)).ToListAsync(ct);

        var missing = normalizedSlugs.Except(tags.Select(t => t.Slug)).ToList();

        if (missing.Count != 0)
            return TypedResults.ValidationProblem(
                errors: new Dictionary<string, string[]>
                {
                    [nameof(EditQuestionRequest.TagSlugs)] = [..missing.Select(s => $"Tag '{s}' does not exist.")]
                });

        question.Edit(request.Title, request.Body, tags, timeProvider.GetUtcNow());

        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}

/// <summary>
/// A full replacement of the editable fields (PUT), so all three are required.
/// </summary>
public sealed record EditQuestionRequest(
    string Title,
    string Body,
    IReadOnlyList<string> TagSlugs)
{
    public sealed class EditQuestionValidator : AbstractValidator<EditQuestionRequest>
    {
        public EditQuestionValidator()
        {
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
