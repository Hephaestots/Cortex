using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Questions;

public sealed class GetQuestionByIdTests(AppFixture fixture)
{
    [Fact]
    public async Task Returns_the_question_without_a_token()
    {
        var created = await fixture.CreateQuestionAsync(tags: ["poetry", "fantasy"], bookId: "OL1W");

        var question = await fixture.GetQuestionAsync(created.Id);

        question.Id.ShouldBe(created.Id);
        question.Title.ShouldBe(created.Title);
        question.Body.ShouldBe(created.Body);
        question.AuthorId.ShouldBe(TestUsers.Author.Id);
        question.AuthorDisplayName.ShouldBe(TestUsers.Author.UserName);
        question.BookId.ShouldBe("OL1W");
        question.Tags.ShouldBe(["fantasy", "poetry"]); // sorted by slug
        question.ViewCount.ShouldBe(1); // this GET
        question.AnswersCount.ShouldBe(0);
        question.Score.ShouldBe(0);
        question.AcceptedAnswerId.ShouldBeNull();
        question.UpdatedAt.ShouldBeNull();
        question.Version.ShouldBe(created.Version);
    }

    [Fact]
    public async Task Each_get_counts_a_view_and_leaves_the_version_alone()
    {
        var created = await fixture.CreateQuestionAsync();

        var first = await fixture.GetQuestionAsync(created.Id);
        var second = await fixture.GetQuestionAsync(created.Id);

        first.ViewCount.ShouldBe(1);
        second.ViewCount.ShouldBe(2);
        // The counter is its own row, so the question's xmin (its version) does not move.
        second.Version.ShouldBe(created.Version);
    }

    /// <summary>
    /// The bug a counter on the questions row caused: every view was a new version, so the
    /// author's edit failed with 409 once anyone else had looked at the question.
    /// </summary>
    [Fact]
    public async Task The_author_can_still_edit_after_other_people_viewed_the_question()
    {
        var created = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        var read = await fixture.GetQuestionAsync(created.Id);
        await fixture.GetQuestionAsync(created.Id);
        await fixture.GetQuestionAsync(created.Id);

        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Author);
            s.Put.Json(new EditQuestionRequest("Still editable", "Body.", [TestData.AnyTag], read.Version))
                .ToUrl($"/api/questions/{created.Id}");
            s.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task Concurrent_views_are_all_counted()
    {
        var created = await fixture.CreateQuestionAsync();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => fixture.GetQuestionAsync(created.Id)));

        // One atomic count = count + 1 per view: no read-modify-write, so no lost updates.
        (await fixture.GetQuestionAsync(created.Id)).ViewCount.ShouldBe(21);
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{Guid.CreateVersion7()}");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task An_id_that_is_not_a_guid_is_404_because_the_route_does_not_match()
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/questions/abc");
            s.StatusCodeShouldBe(404);
        });
    }
}
