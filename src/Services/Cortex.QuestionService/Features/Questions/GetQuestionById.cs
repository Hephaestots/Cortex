using System.Linq.Expressions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Questions;

public static class GetQuestionByIdEndpoint
{
    /// <summary>
    /// Returns one question with its body, and counts the view. Public, no token needed. Answers
    /// have their own endpoint (<c>GET /api/questions/{id}/answers</c>). An id that is not a GUID
    /// (<c>/api/questions/abc</c>) does not match the route, so it is a 404 too.
    /// </summary>
    /// <remarks>
    /// Counting here, not in a separate <c>POST .../views</c> (decided 2026-11-20): fetching the
    /// question is the view, and the client should not need two calls for one action. Accepted
    /// costs: this GET is not side-effect free, so it must not be cached by a shared cache once the
    /// gateway exists (or the cache hits stop counting), and refreshes and bots count as views.
    /// </remarks>
    [WolverineGet("/api/questions/{id:guid}")]
    public static async Task<Results<Ok<QuestionDetail>, NotFound>> Get(
        Guid id,
        QuestionDbContext db,
        CancellationToken ct)
    {
        // First, so the returned count includes this view. No counter row means no question,
        // which saves the second query for a 404.
        if (!await RecordQuestionView.ExecuteAsync(db, id, ct))
            return TypedResults.NotFound();

        // A projection, not FindAsync: only these columns are read, and Score is a SUM in SQL.
        var question = await db.Questions
            .Where(question => question.Id == id)
            .Select(QuestionDetail.Projection)
            .FirstOrDefaultAsync(ct);

        return question is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(question);
    }
}

/// <summary>
/// <see cref="AuthorId"/> lets the client compare it with the token's <c>sub</c> to show Edit;
/// <see cref="AcceptedAnswerId"/> says which answer to mark, not only whether there is one;
/// <see cref="Version"/> goes back with an edit (see <c>IVersioned</c>).
/// </summary>
public sealed record QuestionDetail(
    Guid Id,
    string Title,
    string Body,
    string AuthorId,
    string AuthorDisplayName,
    string? BookId,
    IReadOnlyList<string> Tags,
    int ViewCount,
    int AnswersCount,
    int Score,
    Guid? AcceptedAnswerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint Version)
{
    /// <summary>The EF projection for <c>GetQuestionById</c>; see <c>AnswerDetail.Projection</c> for why an expression.</summary>
    public static readonly Expression<Func<Question, QuestionDetail>> Projection = question => new QuestionDetail(
        question.Id,
        question.Title,
        question.Body,
        question.AuthorId,
        question.AuthorDisplayName,
        question.BookId,
        question.Tags.Select(tag => tag.Slug).OrderBy(slug => slug).ToList(),
        question.ViewCount.Count,
        question.Answers.Count,
        question.Votes.Sum(vote => (int)vote.Value),
        question.AcceptedAnswerId,
        question.CreatedAt,
        question.UpdatedAt,
        question.Version);
}
