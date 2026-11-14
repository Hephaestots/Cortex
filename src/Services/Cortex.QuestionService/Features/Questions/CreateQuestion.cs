using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
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
        // Normalize the slugs passed through the HTTP request.
        var normalizedSlugs = request.TagSlugs
            .Select(Tag.Normalize)
            .ToHashSet();
        
        // Load the existing tags based on the slugs.
        var tags = await db.Tags.Where(tag => normalizedSlugs.Contains(tag.Slug)).ToListAsync(ct);

        // Rejecting unknown tags.
        if (tags.Count < normalizedSlugs.Count)
        {
            var existingSlugs = tags.Select(t => t.Slug).ToHashSet();
            var missingSlugs = normalizedSlugs.Where(slug => !existingSlugs.Contains(slug));
            
            return TypedResults.ValidationProblem(
                errors: new Dictionary<string, string[]>()
                {
                    [nameof(CreateQuestionRequest.TagSlugs)] = [..missingSlugs.Select(s => $"Tag '{s}' does not exist.")]
                });
        }
        
        var question = new Question(
            request.Title,
            request.Body,
            user.Id,
            user.DisplayName ?? throw new InvalidOperationException("User display name must not be null"),
            request.BookId,
            tags,
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
    DateTimeOffset CreatedAt)
{
    public static CreateQuestionResponse From(Question question) => new(
        question.Id,
        question.Title,
        question.Body,
        question.AuthorId,
        question.AuthorDisplayName,
        question.BookId,
        [.. question.Tags.Select(t => t.Slug)],
        question.CreatedAt);
}