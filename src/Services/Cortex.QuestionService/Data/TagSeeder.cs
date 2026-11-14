using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Data;

/// <summary>
/// Inserts the tags from <see cref="TagSeed"/> that the database does not have yet. Existing
/// rows are never updated, so a tag edited in the database keeps its edit.
/// </summary>
/// <remarks>
/// Hooked up through UseSeeding / UseAsyncSeeding, which EF Core runs on every Migrate call:
/// MigrateAsync uses <see cref="SeedAsync"/>, the EF tooling uses <see cref="Seed"/>.
/// </remarks>
public static class TagSeeder
{
    public static void Seed(DbContext context)
    {
        var existingSlugs = context.Set<Tag>().Select(tag => tag.Slug).ToList();
        var missing = MissingTags(existingSlugs);

        if (missing.Count == 0)
            return;

        context.Set<Tag>().AddRange(missing);
        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, CancellationToken ct)
    {
        var existingSlugs = await context.Set<Tag>().Select(tag => tag.Slug).ToListAsync(ct);
        var missing = MissingTags(existingSlugs);

        if (missing.Count == 0)
            return;

        context.Set<Tag>().AddRange(missing);
        await context.SaveChangesAsync(ct);
    }

    // The tags are built first and compared by Tag.Slug, the normalised value that is stored.
    // Comparing the raw seed slug would re-insert an entry such as "Book Clubs" on every start
    // and fail on the unique slug index.
    private static List<Tag> MissingTags(IEnumerable<string> existingSlugs)
    {
        var existing = existingSlugs.ToHashSet();

        return TagSeed.All
            .Select(entry => new Tag(entry.Name, entry.Slug, entry.Description))
            .Where(tag => !existing.Contains(tag.Slug))
            .ToList();
    }
}
