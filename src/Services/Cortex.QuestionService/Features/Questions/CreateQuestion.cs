using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Shared;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Questions;

public static class CreateQuestionEndpoint
{
    [Authorize]
    [WolverinePost("/api/questions")]
    public static async Task<Results<Created<CreateQuestionResponse>, ValidationProblem>> Post(
        CreateQuestionRequest request,
        CurrentUser user,
        QuestionDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var lookup = await TagLookup.FindBySlugsAsync(db, request.TagSlugs, ct);

        if (lookup.HasMissing)
            return Problems.Validation(nameof(CreateQuestionRequest.TagSlugs), lookup.MissingMessages);

        var question = new Question(
            request.Title,
            request.Body,
            user.Id,
            user.DisplayName,
            request.BookId,
            lookup.Tags,
            timeProvider.GetUtcNow());
        
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(
            uri: $"/api/questions/{question.Id}", 
            CreateQuestionResponse.From(question));
    }
}

public sealed record CreateQuestionRequest(
    string Title,
    string Body,
    string? BookId,
    IReadOnlyList<string> TagSlugs)
{
    public class CreateQuestionValidator : AbstractValidator<CreateQuestionRequest>
    {
        public CreateQuestionValidator()
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

            RuleFor(x => x.BookId)
                .MaximumLength(Question.MaxBookIdLength)
                .WithError(Question.Errors.BookIdTooLong);

            RuleFor(x => x.TagSlugs)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(s => s is { Count: >= 1 and <= Question.MaxTags})
                .WithError(Question.Errors.TagsOutOfRange);
            
            RuleForEach(x => x.TagSlugs)
                .NotEmpty()
                .WithError(Tag.Errors.EmptySlug)
                .MaximumLength(Tag.MaxSlugLength)
                .WithError(Tag.Errors.SlugTooLong);
        }
    }
}

public sealed record CreateQuestionResponse(
    Guid Id,
    string Title,
    string Body,
    string AuthorId,
    string AuthorDisplayName,
    string? BookId,
    IReadOnlyList<string> TagSlugs,
    DateTimeOffset CreatedAt,
    uint Version)
{
    public static CreateQuestionResponse From(Question question) => new(
        question.Id,
        question.Title,
        question.Body,
        question.AuthorId,
        question.AuthorDisplayName,
        question.BookId,
        [.. question.Tags.Select(t => t.Slug)],
        question.CreatedAt,
        // Read back by EF from the INSERT, so the client can edit without a GET first.
        question.Version);
}