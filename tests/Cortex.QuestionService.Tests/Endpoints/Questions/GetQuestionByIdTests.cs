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
        question.ViewCount.ShouldBe(0);
        question.AnswersCount.ShouldBe(0);
        question.Score.ShouldBe(0);
        question.AcceptedAnswerId.ShouldBeNull();
        question.UpdatedAt.ShouldBeNull();
        question.Version.ShouldBe(created.Version);
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
