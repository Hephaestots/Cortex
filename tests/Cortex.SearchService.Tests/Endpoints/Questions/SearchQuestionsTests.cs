using Cortex.Contracts.Features.Questions;
using Cortex.SearchService.Features.Questions;
using Cortex.SearchService.Tests.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Tests.Endpoints.Questions;

/// <summary>
/// <c>GET /api/search/questions</c>. Every test indexes its own questions under a tag only it uses
/// (<see cref="TestData.NewTag"/>) and filters on it, so the shared collection never leaks into the
/// results. That also keeps each query unique, which matters because the endpoint asks Typesense to
/// cache results for 60 s: the same query twice in a run would return the cached answer.
/// </summary>
public sealed class SearchQuestionsTests(AppFixture fixture)
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static QuestionCreated Question(string tag, string title = "Any title", int minutesAfterNoon = 0) =>
        TestData.QuestionCreated() with
        {
            Title = title,
            TagSlugs = [tag],
            CreatedAt = Noon.AddMinutes(minutesAfterNoon)
        };

    // --- Filters -------------------------------------------------------------------------------

    [Fact]
    public async Task A_tag_filter_returns_only_the_questions_with_that_tag()
    {
        var tag = TestData.NewTag();
        var tagged = Question(tag);
        var other = Question(TestData.NewTag());
        await fixture.IndexAsync(tagged, other);

        var response = await fixture.SearchAsync($"tags={tag}");

        response.Items.Select(item => item.Id).ShouldBe([tagged.QuestionId]);
        response.Found.ShouldBe(1);
        response.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Several_tags_are_combined_with_and()
    {
        var first = TestData.NewTag();
        var second = TestData.NewTag();
        var both = Question(first) with { TagSlugs = [first, second] };
        var onlyFirst = Question(first);
        await fixture.IndexAsync(both, onlyFirst);

        var response = await fixture.SearchAsync($"tags={first}&tags={second}");

        response.Items.Select(item => item.Id).ShouldBe([both.QuestionId]);
    }

    [Fact]
    public async Task A_tag_typed_with_capitals_and_spaces_still_matches()
    {
        var tag = TestData.NewTag();
        await fixture.IndexAsync(Question(tag));

        // "test-abc" sent as " TEST abc ". The endpoint normalizes it like Tag.Normalize, but
        // Typesense's exact filter also ignores case and treats the space like the "-" separator:
        // with the endpoint's normalization removed this still passes (tried). It pins the behaviour
        // a client sees, not which layer provides it.
        var typed = " " + tag.ToUpperInvariant().Replace('-', ' ') + " ";

        var response = await fixture.SearchAsync($"tags={Uri.EscapeDataString(typed)}");

        response.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_tag_filter_does_not_match_a_longer_tag_that_starts_with_it()
    {
        // token_separators splits "science-fiction" into "science" and "fiction" for search; the
        // filter must still match whole tags only, or ?tags=science would return science fiction.
        var tag = TestData.NewTag();
        var exact = Question(tag);
        var longer = Question($"{tag}-extra");
        await fixture.IndexAsync(exact, longer);

        var response = await fixture.SearchAsync($"tags={tag}");

        response.Items.Select(item => item.Id).ShouldBe([exact.QuestionId]);
    }

    [Fact]
    public async Task A_book_filter_returns_only_the_questions_about_that_book()
    {
        var tag = TestData.NewTag();
        var aboutTheBook = Question(tag) with { BookId = "/works/OL1W" };
        var aboutAnother = Question(tag) with { BookId = "/works/OL2W" };
        var withoutBook = Question(tag) with { BookId = null };
        await fixture.IndexAsync(aboutTheBook, aboutAnother, withoutBook);

        var response = await fixture.SearchAsync($"tags={tag}&bookId={Uri.EscapeDataString("/works/OL1W")}");

        response.Items.Select(item => item.Id).ShouldBe([aboutTheBook.QuestionId]);
    }

    // --- Order ---------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_query_the_newest_question_comes_first()
    {
        var tag = TestData.NewTag();
        var oldest = Question(tag, minutesAfterNoon: 0);
        var newest = Question(tag, minutesAfterNoon: 20);
        var middle = Question(tag, minutesAfterNoon: 10);
        // Indexed out of order, so the result can't just be insertion order.
        await fixture.IndexAsync(middle, oldest, newest);

        var response = await fixture.SearchAsync($"tags={tag}");

        response.Items.Select(item => item.Id).ShouldBe([newest.QuestionId, middle.QuestionId, oldest.QuestionId]);
    }

    [Fact]
    public async Task With_a_query_a_title_match_ranks_above_a_newer_body_match()
    {
        var tag = TestData.NewTag();
        var inTitle = Question(tag, title: "Is the lighthouse real?", minutesAfterNoon: 0);
        var inBody = Question(tag, title: "Another question", minutesAfterNoon: 10) with
        {
            Body = "The lighthouse appears in chapter two."
        };
        await fixture.IndexAsync(inTitle, inBody);

        var response = await fixture.SearchAsync($"q=lighthouse&tags={tag}");

        // Newest first would put the body match first; the title's weight (4 against 1) wins.
        response.Items.Select(item => item.Id).ShouldBe([inTitle.QuestionId, inBody.QuestionId]);
    }

    // --- Results -------------------------------------------------------------------------------

    [Fact]
    public async Task A_hit_carries_the_question_and_its_highlights()
    {
        var tag = TestData.NewTag();
        var created = Question(tag, title: "Where is the lighthouse?") with
        {
            Body = "The **lighthouse** is never named."
        };
        await fixture.IndexAsync(created);

        var response = await fixture.SearchAsync($"q=lighthouse&tags={tag}");

        var hit = response.Items.ShouldHaveSingleItem();
        hit.Id.ShouldBe(created.QuestionId);
        hit.Title.ShouldBe("Where is the lighthouse?");
        hit.Tags.ShouldBe([tag]);
        hit.AuthorDisplayName.ShouldBe(created.AuthorDisplayName);
        hit.BookId.ShouldBe(created.BookId);
        hit.CreatedAt.ShouldBe(created.CreatedAt);
        // \u0001 and \u0002 instead of <mark>: Typesense doesn't escape the text around a highlight,
        // so the frontend renders these markers itself and never treats a snippet as HTML.
        hit.TitleHighlighted.ShouldBe("Where is the \u0001lighthouse\u0002?");
        hit.Snippet.ShouldNotBeNull().ShouldContain("\u0001lighthouse\u0002");
    }

    [Fact]
    public async Task Facets_are_returned_only_when_asked_for()
    {
        var tag = TestData.NewTag();
        var otherTag = TestData.NewTag();
        await fixture.IndexAsync(
            Question(tag) with { TagSlugs = [tag, otherTag] },
            Question(tag));

        var without = await fixture.SearchAsync($"tags={tag}");
        var with = await fixture.SearchAsync($"tags={tag}&facets=true");

        without.Facets.ShouldBeNull();
        var facets = with.Facets.ShouldNotBeNull();
        // Counted over the filtered results: both questions have the tag, one has the other tag.
        var tags = facets.Single(facet => facet.Field == SearchQuestion.Fields.Tags);
        tags.Values.ShouldContain(new SearchFacetValue(tag, 2));
        tags.Values.ShouldContain(new SearchFacetValue(otherTag, 1));
        var accepted = facets.Single(facet => facet.Field == SearchQuestion.Fields.HasAcceptedAnswer);
        accepted.Values.ShouldBe([new SearchFacetValue("false", 2)]);
    }

    // --- Paging --------------------------------------------------------------------------------

    [Fact]
    public async Task A_page_holds_the_rest_of_the_results_with_the_totals()
    {
        var tag = TestData.NewTag();
        var oldest = Question(tag, minutesAfterNoon: 0);
        await fixture.IndexAsync(oldest, Question(tag, minutesAfterNoon: 10), Question(tag, minutesAfterNoon: 20));

        var response = await fixture.SearchAsync($"tags={tag}&page=2&pageSize=2");

        response.Items.Select(item => item.Id).ShouldBe([oldest.QuestionId]);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(2);
        response.TotalCount.ShouldBe(3);
        response.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Paging_stops_at_1000_results_while_found_counts_every_match()
    {
        var tag = TestData.NewTag();
        const int matches = SearchQuestionsQuery.MaxResults + 1;

        // Imported in one call straight into Typesense: 1,001 trips through the handler would be slow,
        // and the handler is not what this test is about.
        var documents = Enumerable.Range(0, matches).Select(i => new SearchQuestion
        {
            Id = Guid.CreateVersion7().ToString(),
            Title = $"Question {i}",
            Tags = [tag],
            AuthorId = "author-1",
            AuthorDisplayName = "reader",
            CreatedAt = Noon.ToUnixTimeSeconds() + i,
            LastActivityAt = Noon.ToUnixTimeSeconds() + i
        });
        var import = await fixture.Typesense.ImportDocuments(
            SearchQuestion.CollectionName,
            documents,
            matches,
            ImportType.Create,
            null,
            null,
            TestContext.Current.CancellationToken);
        import.ShouldAllBe(line => line.Success);

        // The deepest reachable page: 50 × 20 = 1,000.
        var lastPage = await fixture.SearchAsync($"tags={tag}&page=50&pageSize=20");

        lastPage.Found.ShouldBe(matches);
        lastPage.TotalCount.ShouldBe(SearchQuestionsQuery.MaxResults);
        lastPage.TotalPages.ShouldBe(50);
        lastPage.Items.Count.ShouldBe(20);
    }

    // --- Validation ----------------------------------------------------------------------------

    [Theory]
    // One past the 1,000 reachable results: 21 × 50 = 1,050.
    [InlineData("page=21&pageSize=50", "Page")]
    [InlineData("pageSize=51", "PageSize")]
    [InlineData("page=0", "Page")]
    [InlineData("tags=a&tags=b&tags=c&tags=d&tags=e&tags=f", "Tags")]
    // A backtick would end the escaped value in the Typesense filter and let the caller add their own.
    [InlineData("tags=a%60b", "Tags[0]")]
    [InlineData("bookId=%60%20||%20id:*", "BookId")]
    public async Task An_invalid_query_is_a_400_under_its_parameter(string query, string key)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/search/questions?{query}");
            s.StatusCodeShouldBe(400);
        });

        var problem = await result.ReadProblemAsync();
        problem.ErrorsFor(key).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_query_longer_than_200_characters_is_a_400()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/search/questions?q={new string('a', 201)}");
            s.StatusCodeShouldBe(400);
        });

        var problem = await result.ReadProblemAsync();
        problem.ErrorsFor("Q").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task An_unparseable_number_is_a_400()
    {
        // RejectUnparseableQueryValues: without it Wolverine would quietly use page 1.
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/search/questions?page=abc");
            s.StatusCodeShouldBe(400);
        });
    }
}
