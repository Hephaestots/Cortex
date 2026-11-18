using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Features.Shared;

/// <summary>
/// Turns the tag slugs a client sends into tracked <see cref="Tag"/> entities, for every slice
/// that sets a question's tags (create, edit).
/// </summary>
/// <remarks>
/// A static helper that takes the context, not a registered service: endpoints keep method
/// injection, and Wolverine has nothing new to construct.
/// </remarks>
public static class TagLookup
{
    /// <summary>
    /// Normalizes the slugs with <see cref="Tag.Normalize"/>, drops duplicates and loads the
    /// matching tags in one query. The tags are tracked, so EF only writes the join rows.
    /// </summary>
    public static async Task<TagLookupResult> FindBySlugsAsync(
        QuestionDbContext db, IEnumerable<string> slugs, CancellationToken ct)
    {
        var normalizedSlugs = slugs
            .Select(Tag.Normalize)
            .ToHashSet();

        var tags = await db.Tags
            .Where(tag => normalizedSlugs.Contains(tag.Slug))
            .ToListAsync(ct);

        var missing = normalizedSlugs
            .Except(tags.Select(tag => tag.Slug))
            .Order()
            .ToList();

        return new TagLookupResult(tags, missing);
    }
}

public sealed record TagLookupResult(IReadOnlyList<Tag> Tags, IReadOnlyList<string> MissingSlugs)
{
    public bool HasMissing => MissingSlugs.Count != 0;

    /// <summary>One message per unknown slug, for a validation problem.</summary>
    public IEnumerable<string> MissingMessages =>
        MissingSlugs.Select(slug => $"Tag '{slug}' does not exist.");
}
