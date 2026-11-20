using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Questions;

/// <summary>Status order (see the endpoint): 401, 400 for the version, 404, 403, 409.</summary>
public sealed class DeleteQuestionTests(AppFixture fixture)
{
    private Task<IScenarioResult> DeleteAsync(Guid id, string query, TestUser? user, int expectedStatus) =>
        fixture.Host.Scenario(s =>
        {
            if (user is not null)
                s.As(user);

            s.Delete.Url($"/api/questions/{id}{query}");
            s.StatusCodeShouldBe(expectedStatus);
        });

    private static string VersionOf(CreateQuestionResponse question) => $"?version={question.Version}";

    [Fact]
    public async Task Without_a_user_is_401()
    {
        var question = await fixture.CreateQuestionAsync();

        await DeleteAsync(question.Id, VersionOf(question), user: null, 401);
    }

    [Fact]
    public async Task The_author_deletes_it_and_it_is_gone()
    {
        var question = await fixture.CreateQuestionAsync();

        await DeleteAsync(question.Id, VersionOf(question), TestUsers.Author, 204);

        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/questions/{question.Id}");
            s.StatusCodeShouldBe(404);
        });
        await DeleteAsync(question.Id, VersionOf(question), TestUsers.Author, 404);
    }

    [Fact]
    public async Task An_admin_may_delete_someone_elses_question()
    {
        var question = await fixture.CreateQuestionAsync();

        await DeleteAsync(question.Id, VersionOf(question), TestUsers.Admin, 204);
    }

    [Fact]
    public async Task Another_user_is_403_with_the_delete_forbidden_code()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await DeleteAsync(question.Id, VersionOf(question), TestUsers.Reader, 403);

        (await result.ReadProblemAsync()).Code.ShouldBe("question.delete_forbidden");
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        await DeleteAsync(Guid.CreateVersion7(), "?version=1", TestUsers.Author, 404);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?version=abc")]
    public async Task A_missing_or_unparseable_version_is_400(string query)
    {
        var question = await fixture.CreateQuestionAsync();

        await DeleteAsync(question.Id, query, TestUsers.Author, 400);
    }

    [Fact]
    public async Task A_stale_version_is_409_and_the_question_stays()
    {
        var question = await fixture.CreateQuestionAsync();
        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Author);
            s.Put.Json(new EditQuestionRequest("Edited", "Edited.", ["fantasy"], question.Version))
                .ToUrl($"/api/questions/{question.Id}");
            s.StatusCodeShouldBe(204);
        });

        var result = await DeleteAsync(question.Id, VersionOf(question), TestUsers.Author, 409);

        (await result.ReadProblemAsync()).Code.ShouldBe("concurrency.stale_version");
        await fixture.GetQuestionAsync(question.Id);
    }

    /// <summary>
    /// The endpoint loads only the question row and leaves the rest to Postgres's cascades.
    /// An accepted answer is the case that would break if EF deleted the answers itself
    /// (<c>fk_questions_accepted_answer</c> is RESTRICT).
    /// </summary>
    [Fact]
    public async Task Deleting_cascades_to_answers_votes_and_tag_links_but_keeps_the_tags()
    {
        var tag = await fixture.CreateTagAsync();
        var question = await fixture.CreateQuestionAsync(tags: [tag]);
        var answer = await AnswerAsync(question.Id, TestUsers.Reader);
        await AnswerAsync(question.Id, TestUsers.Admin);

        // No endpoints yet for votes and accepting, so they go in through the domain.
        var now = DateTimeOffset.UtcNow;
        await fixture.WithDbAsync(async db =>
        {
            var loaded = await db.Questions.Include(q => q.Answers).SingleAsync(q => q.Id == question.Id);
            var accepted = loaded.Answers.Single(a => a.Id == answer.Id);

            loaded.AcceptAnswer(accepted, TestUsers.Author.Id);
            db.QuestionVotes.Add(QuestionVote.Cast(loaded, TestUsers.Reader.Id, VoteValue.Up, now));
            db.AnswerVotes.Add(AnswerVote.Cast(accepted, TestUsers.Author.Id, VoteValue.Up, now));
            await db.SaveChangesAsync();
        });

        // Accepting changed the question row, so its version did too.
        var current = await fixture.GetQuestionAsync(question.Id);
        await DeleteAsync(question.Id, $"?version={current.Version}", TestUsers.Author, 204);

        await fixture.WithDbAsync(async db =>
        {
            (await db.Answers.CountAsync(a => a.QuestionId == question.Id)).ShouldBe(0);
            (await db.QuestionVotes.CountAsync(v => v.QuestionId == question.Id)).ShouldBe(0);
            (await db.AnswerVotes.CountAsync(v => v.AnswerId == answer.Id)).ShouldBe(0);
            (await db.Tags.Where(t => t.Slug == tag).SelectMany(t => t.Questions).CountAsync()).ShouldBe(0);
            (await db.Tags.CountAsync(t => t.Slug == tag)).ShouldBe(1);
        });
    }

    private async Task<CreateAnswerResponse> AnswerAsync(Guid questionId, TestUser user)
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.As(user);
            s.Post.Json(new CreateAnswerRequest("An answer.")).ToUrl($"/api/questions/{questionId}/answers");
            s.StatusCodeShouldBe(201);
        });

        return await result.ReadAsJsonAsync<CreateAnswerResponse>()
               ?? throw new InvalidOperationException("POST .../answers returned no body.");
    }
}
