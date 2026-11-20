using System.Linq.Expressions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Answers;

public static class GetAnswerByIdEndpoint
{
    /// <summary>
    /// Returns one answer. Public, no token needed. Answer routes are flat (<c>/api/answers/{id}</c>):
    /// an answer id is unique on its own, and it is where <c>CreateAnswer</c>'s <c>Location</c> points.
    /// </summary>
    [WolverineGet("/api/answers/{id:guid}")]
    public static async Task<Results<Ok<AnswerDetail>, NotFound>> Get(
        Guid id,
        QuestionDbContext db,
        CancellationToken ct)
    {
        var answer = await db.Answers
            .Where(answer => answer.Id == id)
            .Select(AnswerDetail.Projection)
            .FirstOrDefaultAsync(ct);

        return answer is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(answer);
    }
}

/// <summary>
/// One answer as the client shows it, in <c>GetAnswerById</c> and in each row of <c>GetAnswers</c>
/// (an answer is always shown in full, so the list and the single read share the shape).
/// <see cref="AuthorId"/> lets the client show Edit and Delete; <see cref="Version"/> goes back
/// with an edit or a delete.
/// </summary>
public sealed record AnswerDetail(
    Guid Id,
    Guid QuestionId,
    string Body,
    string AuthorId,
    string AuthorDisplayName,
    int Score,
    bool IsAccepted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint Version)
{
    /// <summary>
    /// The projection both reads use. An expression, not a <c>From(answer)</c> method: EF can
    /// translate an expression to SQL (only these columns, <c>Score</c> as a <c>SUM</c>,
    /// <c>IsAccepted</c> through a join), while it can't see into a method and would load the entity.
    /// </summary>
    public static readonly Expression<Func<Answer, AnswerDetail>> Projection = answer => new AnswerDetail(
        answer.Id,
        answer.QuestionId,
        answer.Body,
        answer.AuthorId,
        answer.AuthorDisplayName,
        answer.Votes.Sum(vote => (int)vote.Value),
        answer.Question.AcceptedAnswerId == answer.Id,
        answer.CreatedAt,
        answer.UpdatedAt,
        answer.Version);
}
