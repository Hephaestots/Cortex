using Cortex.QuestionService.Data;
using Cortex.QuestionService.Features.Tags;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Tags;

/// <summary>
/// The smoke test for the whole setup: if this passes, the container started, the migrations
/// and the tag seeding ran through the normal startup path, and the host answers HTTP.
/// </summary>
/// <remarks>
/// Other tests add their own tags (<see cref="TestData.CreateTagAsync"/>), so the checks are
/// about the seeded tags being there and in order, not about the list holding only them.
/// </remarks>
public sealed class GetTagsTests(AppFixture fixture)
{
    [Fact]
    public async Task Returns_every_seeded_tag_sorted_by_name_without_a_token()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/tags");
            s.StatusCodeShouldBeOk();
        });

        var tags = await result.ReadAsJsonAsync<List<TagSummary>>();
        var seededSlugs = TagSeed.All.Select(t => t.Slug).ToHashSet();

        tags.ShouldNotBeNull();
        seededSlugs.ShouldBeSubsetOf(tags.Select(t => t.Slug));
        tags.Where(t => seededSlugs.Contains(t.Slug))
            .Select(t => t.Name)
            .ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal);
    }
}
