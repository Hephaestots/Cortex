using Cortex.QuestionService.Data;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Features.Questions;

/// <summary>
/// Counts one view of a question. The only place the count changes, so step 4 can swap the
/// direct call for a <c>QuestionViewed</c> message without touching anything else.
/// </summary>
/// <remarks>
/// <para>
/// One atomic statement, <c>UPDATE question_view_counts SET count = count + 1 WHERE question_id = @id</c>:
/// concurrent views never lose a count, nothing is loaded or tracked, and only the counter row is
/// written, so the question's version (<c>xmin</c> of <c>questions</c>) does not change.
/// </para>
/// <para>
/// Step 4 plan (see "ViewCount" in docs/status.md): <c>GetQuestionById</c> publishes
/// <c>QuestionViewed(QuestionId, ViewerId?, ViewedAt)</c> to a local, non-durable Wolverine queue
/// instead (a lost view does not matter, and the outbox would cost one insert per view, as much as
/// the increment), and a handler calls this, later batched or deduplicated per viewer.
/// </para>
/// </remarks>
public static class RecordQuestionView
{
    /// <returns>
    /// Whether the question exists: 0 rows updated means there is no counter row, so no question.
    /// </returns>
    public static async Task<bool> ExecuteAsync(QuestionDbContext db, Guid questionId, CancellationToken ct) =>
        await db.QuestionViewCounts
            .Where(viewCount => viewCount.QuestionId == questionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                viewCount => viewCount.Count,
                viewCount => viewCount.Count + 1), ct) > 0;
}
