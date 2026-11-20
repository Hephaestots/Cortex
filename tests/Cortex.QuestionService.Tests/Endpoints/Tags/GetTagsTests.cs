using Cortex.QuestionService.Data;
using Cortex.QuestionService.Features.Tags;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Tags;

/// <summary>
/// The smoke test for the whole setup: if this passes, the container started, the migrations
/// and the tag seeding ran through the normal startup path, and the host answers HTTP.
/// </summary>
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

        tags.ShouldNotBeNull();
        tags.Select(t => t.Slug).ShouldBe(TagSeed.All.Select(t => t.Slug), ignoreOrder: true);
        tags.Select(t => t.Name).ShouldBeInOrder(SortDirection.Ascending, StringComparer.Ordinal);
    }
}
