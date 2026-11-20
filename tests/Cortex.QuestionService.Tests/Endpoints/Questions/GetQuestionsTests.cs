using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Features.Shared;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Questions;

/// <summary>
/// Every test that counts or pages filters on a tag it created itself
/// (<see cref="TestData.CreateTagAsync"/>), so questions from other tests never show up.
/// </summary>
public sealed class GetQuestionsTests(AppFixture fixture)
{
    private async Task<PagedResponse<QuestionSummary>> GetAsync(string query)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions{query}");
            s.StatusCodeShouldBeOk();
        });

        return await result.ReadAsJsonAsync<PagedResponse<QuestionSummary>>()
               ?? throw new InvalidOperationException("GET /api/questions returned no body.");
    }

    /// <summary>Creates <paramref name="count"/> questions with the tag, oldest first.</summary>
    private async Task<List<CreateQuestionResponse>> CreateQuestionsAsync(string tag, int count)
    {
        var created = new List<CreateQuestionResponse>();

        for (var i = 0; i < count; i++)
            created.Add(await fixture.CreateQuestionAsync(tags: [tag]));

        return created;
    }

    [Fact]
    public async Task Pages_newest_first_with_the_total_for_the_filter()
    {
        var tag = await fixture.CreateTagAsync();
        var created = await CreateQuestionsAsync(tag, 5);
        var newestFirst = created.Select(q => q.Id).Reverse().ToList();

        var first = await GetAsync($"?tags={tag}&pageSize=2");
        var second = await GetAsync($"?tags={tag}&page=2&pageSize=2");
        var last = await GetAsync($"?tags={tag}&page=3&pageSize=2");

        first.Items.Select(q => q.Id).ShouldBe(newestFirst[..2]);
        second.Items.Select(q => q.Id).ShouldBe(newestFirst[2..4]);
        last.Items.Select(q => q.Id).ShouldBe(newestFirst[4..]);

        first.Page.ShouldBe(1);
        second.Page.ShouldBe(2);
        first.PageSize.ShouldBe(2);
        first.TotalCount.ShouldBe(5);
        first.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task Several_tags_are_combined_with_AND()
    {
        var mine = await fixture.CreateTagAsync();
        var other = await fixture.CreateTagAsync();
        var onlyMine = await fixture.CreateQuestionAsync(tags: [mine]);
        var both = await fixture.CreateQuestionAsync(tags: [mine, other]);

        var page = await GetAsync($"?tags={mine}&tags={other}");

        page.Items.Select(q => q.Id).ShouldBe([both.Id]);
        page.Items.ShouldNotContain(q => q.Id == onlyMine.Id);
    }

    [Fact]
    public async Task The_summary_carries_the_list_fields()
    {
        var tag = await fixture.CreateTagAsync();
        var created = await fixture.CreateQuestionAsync(tags: [tag, "poetry"], bookId: "OL2W");

        var summary = (await GetAsync($"?tags={tag}")).Items.ShouldHaveSingleItem();

        summary.Id.ShouldBe(created.Id);
        summary.Title.ShouldBe(created.Title);
        summary.AuthorDisplayName.ShouldBe(TestUsers.Author.UserName);
        summary.BookId.ShouldBe("OL2W");
        summary.Tags.ShouldBe(new[] { "poetry", tag }.Order());
        summary.AnswersCount.ShouldBe(0);
        summary.Score.ShouldBe(0);
        summary.HasAcceptedAnswer.ShouldBeFalse();
    }

    [Fact]
    public async Task Tags_and_query_keys_match_case_insensitively()
    {
        var tag = await fixture.CreateTagAsync();
        var created = await fixture.CreateQuestionAsync(tags: [tag]);

        var page = await GetAsync($"?TAGS={tag.ToUpperInvariant()}&PAGESIZE=1");

        page.PageSize.ShouldBe(1);
        page.Items.Select(q => q.Id).ShouldBe([created.Id]);
    }

    [Theory]
    [InlineData("?tags=no-such-tag")]
    // Not split on commas: this is the single tag "fantasy,horror", which does not exist.
    [InlineData("?tags=fantasy,horror")]
    public async Task A_tag_nobody_has_gives_an_empty_page_not_a_404(string query)
    {
        var page = await GetAsync(query);

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("?page=0", "Page")]
    [InlineData("?page=99999999", "Page")]
    [InlineData("?pageSize=0", "PageSize")]
    [InlineData("?pageSize=51", "PageSize")]
    [InlineData("?tags=a&tags=b&tags=c&tags=d&tags=e&tags=f", "Tags")]
    public async Task Out_of_range_values_are_400(string query, string key)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions{query}");
            s.StatusCodeShouldBe(400);
        });

        (await result.ReadProblemAsync()).ErrorsFor(key).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task An_unparseable_number_is_400_not_the_default()
    {
        // RejectUnparseableQueryValues; by default Wolverine would silently use page 1.
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/questions?page=abc");
            s.StatusCodeShouldBe(400);
        });
    }
}
