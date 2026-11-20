using System.Linq.Expressions;
using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Tags;

public static class GetTagsEndpoint
{
    /// <summary>
    /// Returns every tag, ordered by name, for the client to use as master data (tag pickers,
    /// labels). Public, no token needed. Not paged: the list is the seeded set, a few dozen rows.
    /// </summary>
    [WolverineGet("/api/tags")]
    public static async Task<Ok<IReadOnlyList<TagSummary>>> Get(
        QuestionDbContext db,
        CancellationToken ct)
    {
        var tags = await db.Tags
            // The slug is unique, so it makes the order stable if two names ever compare equal.
            .OrderBy(tag => tag.Name)
            .ThenBy(tag => tag.Slug)
            .Select(TagSummary.Projection)
            .ToListAsync(ct);

        return TypedResults.Ok<IReadOnlyList<TagSummary>>(tags);
    }
}

/// <summary>
/// No id: the slug is the tag's public identifier, and it is what <c>CreateQuestion</c>
/// takes and <c>GetQuestions</c> filters on.
/// </summary>
public sealed record TagSummary(
    string Slug,
    string Name,
    string Description)
{
    /// <summary>The EF projection for <c>GetTags</c>; see <c>AnswerDetail.Projection</c> for why an expression.</summary>
    public static readonly Expression<Func<Tag, TagSummary>> Projection =
        tag => new TagSummary(tag.Slug, tag.Name, tag.Description);
}
