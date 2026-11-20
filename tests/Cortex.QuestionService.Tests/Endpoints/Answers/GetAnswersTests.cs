using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Features.Shared;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Answers;

/// <summary>
/// Each test lists the answers of a question it created, so other tests never show up here.
/// </summary>
public sealed class GetAnswersTests(AppFixture fixture)
{
    private async Task<PagedResponse<AnswerDetail>> GetAsync(Guid questionId, string query = "")
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{questionId}/answers{query}");
            s.StatusCodeShouldBeOk();
        });

        return await result.ReadAsJsonAsync<PagedResponse<AnswerDetail>>()
               ?? throw new InvalidOperationException("GET .../answers returned no body.");
    }

    [Fact]
    public async Task A_question_without_answers_gives_an_empty_page_without_a_token()
    {
        var question = await fixture.CreateQuestionAsync();

        var page = await GetAsync(question.Id);

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        page.PageSize.ShouldBe(PagedQuery.DefaultPageSize);
    }

    [Fact]
    public async Task Accepted_first_then_score_then_oldest()
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        var oldest = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader, "Oldest, no votes.");
        var upvoted = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader, "One up vote.");
        var accepted = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader, "Accepted, down voted.");
        var newest = await fixture.CreateAnswerAsync(question.Id, TestUsers.Admin, "Newest, no votes.");
        var now = DateTimeOffset.UtcNow;

        await fixture.WithDbAsync(async db =>
        {
            var loaded = await db.Questions.Include(q => q.Answers).SingleAsync(q => q.Id == question.Id);
            var byId = loaded.Answers.ToDictionary(a => a.Id);

            loaded.AcceptAnswer(byId[accepted.Id], TestUsers.Author.Id);
            db.AnswerVotes.Add(AnswerVote.Cast(byId[upvoted.Id], TestUsers.Author.Id, VoteValue.Up, now));
            db.AnswerVotes.Add(AnswerVote.Cast(byId[accepted.Id], TestUsers.Author.Id, VoteValue.Down, now));
            await db.SaveChangesAsync();
        });

        var page = await GetAsync(question.Id);

        page.Items.Select(a => a.Id).ShouldBe([accepted.Id, upvoted.Id, oldest.Id, newest.Id]);
        page.Items[0].IsAccepted.ShouldBeTrue();
        page.Items[0].Score.ShouldBe(-1);
        page.Items[1].Score.ShouldBe(1);
        page.TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task Pages_with_the_total_for_the_question()
    {
        var question = await fixture.CreateQuestionAsync();
        var created = new List<Guid>();
        for (var i = 0; i < 3; i++)
            created.Add((await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader)).Id);

        var first = await GetAsync(question.Id, "?pageSize=2");
        var second = await GetAsync(question.Id, "?page=2&pageSize=2");

        first.Items.Select(a => a.Id).ShouldBe(created[..2]);
        second.Items.Select(a => a.Id).ShouldBe(created[2..]);
        first.TotalCount.ShouldBe(3);
        first.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Only_this_questions_answers_are_listed()
    {
        var question = await fixture.CreateQuestionAsync();
        var other = await fixture.CreateQuestionAsync();
        var mine = await fixture.CreateAnswerAsync(question.Id);
        await fixture.CreateAnswerAsync(other.Id);

        (await GetAsync(question.Id)).Items.Select(a => a.Id).ShouldBe([mine.Id]);
    }

    [Theory]
    [InlineData("0199a000-0000-7000-8000-000000000000")]
    [InlineData("abc")]
    public async Task An_unknown_or_malformed_question_id_is_404(string id)
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{id}/answers");
            s.StatusCodeShouldBe(404);
        });
    }

    [Theory]
    [InlineData("?page=0", "Page")]
    [InlineData("?pageSize=51", "PageSize")]
    public async Task Out_of_range_paging_is_400(string query, string key)
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{question.Id}/answers{query}");
            s.StatusCodeShouldBe(400);
        });

        (await result.ReadProblemAsync()).ErrorsFor(key).ShouldNotBeEmpty();
    }
}
