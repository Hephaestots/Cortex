using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Answers;

/// <summary>Status order (see the endpoint): 401, 400 for the version, 404, 403, 409.</summary>
public sealed class DeleteAnswerTests(AppFixture fixture)
{
    private async Task<(Guid QuestionId, CreateAnswerResponse Answer)> NewAnswerAsync(TestUser? author = null)
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        return (question.Id, await fixture.CreateAnswerAsync(question.Id, author ?? TestUsers.Reader));
    }

    private Task<IScenarioResult> DeleteAsync(Guid id, string query, TestUser? user, int expectedStatus) =>
        fixture.Host.Scenario(s =>
        {
            if (user is not null)
                s.As(user);

            s.Delete.Url($"/api/answers/{id}{query}");
            s.StatusCodeShouldBe(expectedStatus);
        });

    private static string VersionOf(CreateAnswerResponse answer) => $"?version={answer.Version}";

    private Task AnswerShouldBeGoneAsync(Guid id) =>
        fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/answers/{id}");
            s.StatusCodeShouldBe(404);
        });

    [Fact]
    public async Task Without_a_user_is_401()
    {
        var (_, answer) = await NewAnswerAsync();

        await DeleteAsync(answer.Id, VersionOf(answer), user: null, 401);
    }

    [Fact]
    public async Task The_author_deletes_it_and_it_is_gone()
    {
        var (questionId, answer) = await NewAnswerAsync(TestUsers.Reader);

        await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Reader, 204);

        await AnswerShouldBeGoneAsync(answer.Id);
        (await fixture.GetQuestionAsync(questionId)).AnswersCount.ShouldBe(0);
        await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Reader, 404);
    }

    [Fact]
    public async Task An_admin_may_delete_someone_elses_answer()
    {
        var (_, answer) = await NewAnswerAsync(TestUsers.Reader);

        await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Admin, 204);

        await AnswerShouldBeGoneAsync(answer.Id);
    }

    [Fact]
    public async Task The_questions_author_is_403_with_the_delete_forbidden_code()
    {
        var (_, answer) = await NewAnswerAsync(TestUsers.Reader);

        var result = await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Author, 403);

        (await result.ReadProblemAsync()).Code.ShouldBe("answer.delete_forbidden");
        await fixture.GetAnswerAsync(answer.Id);
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        await DeleteAsync(Guid.CreateVersion7(), "?version=1", TestUsers.Reader, 404);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?version=abc")]
    public async Task A_missing_or_unparseable_version_is_400(string query)
    {
        var (_, answer) = await NewAnswerAsync();

        await DeleteAsync(answer.Id, query, TestUsers.Reader, 400);
    }

    [Fact]
    public async Task A_stale_version_is_409_and_the_answer_stays()
    {
        var (_, answer) = await NewAnswerAsync(TestUsers.Reader);
        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Put.Json(new EditAnswerRequest("Edited.", answer.Version)).ToUrl($"/api/answers/{answer.Id}");
            s.StatusCodeShouldBe(204);
        });

        var result = await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Reader, 409);

        (await result.ReadProblemAsync()).Code.ShouldBe("concurrency.stale_version");
        await fixture.GetAnswerAsync(answer.Id);
    }

    /// <summary>
    /// The case <c>fk_questions_accepted_answer</c> (RESTRICT) makes interesting: EF must clear the
    /// question's <c>accepted_answer_id</c> before it deletes the answer row, or Postgres refuses.
    /// </summary>
    [Fact]
    public async Task Deleting_the_accepted_answer_clears_it_on_the_question_and_removes_its_votes()
    {
        var (questionId, answer) = await NewAnswerAsync(TestUsers.Reader);
        var other = await fixture.CreateAnswerAsync(questionId, TestUsers.Admin);
        var now = DateTimeOffset.UtcNow;

        await fixture.WithDbAsync(async db =>
        {
            var loaded = await db.Questions.Include(q => q.Answers).SingleAsync(q => q.Id == questionId);
            var accepted = loaded.Answers.Single(a => a.Id == answer.Id);

            loaded.AcceptAnswer(accepted, TestUsers.Author.Id);
            db.AnswerVotes.Add(AnswerVote.Cast(accepted, TestUsers.Author.Id, VoteValue.Up, now));
            await db.SaveChangesAsync();
        });
        (await fixture.GetQuestionAsync(questionId)).AcceptedAnswerId.ShouldBe(answer.Id);

        await DeleteAsync(answer.Id, VersionOf(answer), TestUsers.Reader, 204);

        var question = await fixture.GetQuestionAsync(questionId);
        question.AcceptedAnswerId.ShouldBeNull();
        question.AnswersCount.ShouldBe(1);
        await fixture.GetAnswerAsync(other.Id);
        (await fixture.WithDbAsync(db => db.AnswerVotes.CountAsync(v => v.AnswerId == answer.Id))).ShouldBe(0);
    }
}
