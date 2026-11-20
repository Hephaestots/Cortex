using System.Linq.Expressions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Shared;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Questions;

public static class GetQuestionsEndpoint
{
    /// <summary>
    /// Lists questions, newest first, one page at a time. Public, no token needed.
    /// <c>?tags=a&amp;tags=b</c> keeps only questions that have <b>all</b> the given tags (AND).
    /// An unknown tag gives an empty page, not a 404.
    /// </summary>
    [WolverineGet("/api/questions")]
    public static async Task<Ok<PagedResponse<QuestionSummary>>> Get(
        [FromQuery] GetQuestionsQuery query,
        QuestionDbContext db,
        CancellationToken ct)
    {
        // Normalised the same way as on create; empty values (?tags=) mean "no filter".
        var slugs = query.Tags
            .Select(Tag.Normalize)
            .Where(slug => slug.Length > 0)
            .Distinct()
            .ToList();

        var questions = db.Questions.AsQueryable();

        // One EXISTS per tag, joined with AND.
        foreach (var slug in slugs)
            questions = questions.Where(question => question.Tags.Any(tag => tag.Slug == slug));

        // Counted before paging, so it is the total for the filter, not for this page.
        var totalCount = await questions.CountAsync(ct);

        var items = await questions
            // Id breaks ties, so a question never shows on two pages or on none.
            .OrderByDescending(question => question.CreatedAt)
            .ThenByDescending(question => question.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(QuestionSummary.Projection)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<QuestionSummary>(items, query.Page, query.PageSize, totalCount));
    }
}

public sealed class GetQuestionsQuery : PagedQuery
{
    public string[] Tags { get; set; } = [];

    public sealed class GetQuestionsQueryValidator : PagedQueryValidator<GetQuestionsQuery>
    {
        public GetQuestionsQueryValidator()
        {
            // Not a domain rule (Question.Errors.TagsOutOfRange speaks about creating a question),
            // but the same limit: with AND, more tags than a question can hold never matches.
            RuleFor(x => x.Tags)
                .Must(tags => tags.Length <= Question.MaxTags)
                .WithMessage($"You can filter by at most {Question.MaxTags} tags.");

            RuleForEach(x => x.Tags)
                .MaximumLength(Tag.MaxSlugLength)
                .WithError(Tag.Errors.SlugTooLong);
        }
    }
}

public sealed record QuestionSummary(
    Guid Id,
    string Title,
    string AuthorDisplayName,
    string? BookId,
    IReadOnlyList<string> Tags,
    int AnswersCount,
    int Score,
    bool HasAcceptedAnswer,
    DateTimeOffset CreatedAt)
{
    /// <summary>The EF projection for <c>GetQuestions</c>; see <c>AnswerDetail.Projection</c> for why an expression.</summary>
    public static readonly Expression<Func<Question, QuestionSummary>> Projection = question => new QuestionSummary(
        question.Id,
        question.Title,
        question.AuthorDisplayName,
        question.BookId,
        question.Tags.Select(tag => tag.Slug).OrderBy(slug => slug).ToList(),
        question.Answers.Count,
        question.Votes.Sum(vote => (int)vote.Value),
        question.AcceptedAnswerId != null,
        question.CreatedAt);
}
