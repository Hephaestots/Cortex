using Cortex.QuestionService.Data;
using Cortex.QuestionService.Features.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Answers;

public static class GetAnswersEndpoint
{
    /// <summary>
    /// The answers to one question, one page at a time. Public, no token needed. Nested under
    /// the question because an answer list only exists for its question ("real ownership", see
    /// "<c>GetQuestions</c>" in docs/status.md). A question that does not exist is a 404, not an
    /// empty page: the parent is missing, unlike a filter that matches nothing.
    /// </summary>
    /// <remarks>
    /// Order: the accepted answer first, then the highest score, then the oldest (the first to
    /// answer wins a tie), and the id last so the order is total and paging stable. The question
    /// detail does not embed answers: projecting tags and answers together would multiply rows,
    /// and this keeps the question read small.
    /// </remarks>
    [WolverineGet("/api/questions/{id:guid}/answers")]
    public static async Task<Results<Ok<PagedResponse<AnswerDetail>>, NotFound>> Get(
        Guid id,
        [FromQuery] GetAnswersQuery query,
        QuestionDbContext db,
        CancellationToken ct)
    {
        if (!await db.Questions.AnyAsync(question => question.Id == id, ct))
            return TypedResults.NotFound();

        var answers = db.Answers.Where(answer => answer.QuestionId == id);

        var totalCount = await answers.CountAsync(ct);

        var items = await answers
            .OrderByDescending(answer => answer.Question.AcceptedAnswerId == answer.Id)
            .ThenByDescending(answer => answer.Votes.Sum(vote => (int)vote.Value))
            .ThenBy(answer => answer.CreatedAt)
            .ThenBy(answer => answer.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(AnswerDetail.Projection)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<AnswerDetail>(items, query.Page, query.PageSize, totalCount));
    }
}

/// <summary><c>?page=&amp;pageSize=</c> with the shared defaults and limits; no filters.</summary>
public sealed class GetAnswersQuery : PagedQuery
{
    /// <summary>Only the paging rules; a concrete class so Wolverine registers it.</summary>
    public sealed class GetAnswersQueryValidator : PagedQueryValidator<GetAnswersQuery>;
}
