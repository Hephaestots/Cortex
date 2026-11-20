using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Answers;

public sealed class GetAnswerByIdTests(AppFixture fixture)
{
    [Fact]
    public async Task Returns_the_answer_without_a_token_and_matches_its_location()
    {
        var question = await fixture.CreateQuestionAsync();
        var created = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader);

        var answer = await fixture.GetAnswerAsync(created.Id);

        answer.Id.ShouldBe(created.Id);
        answer.QuestionId.ShouldBe(question.Id);
        answer.Body.ShouldBe(created.Body);
        answer.AuthorId.ShouldBe(TestUsers.Reader.Id);
        answer.AuthorDisplayName.ShouldBe(TestUsers.Reader.UserName);
        answer.Score.ShouldBe(0);
        answer.IsAccepted.ShouldBeFalse();
        // Postgres keeps microseconds, .NET ticks are 100 ns: the create response can be up to 1 µs
        // more precise than what is stored (see "Known leftovers" in docs/status.md).
        answer.CreatedAt.ShouldBe(created.CreatedAt, TimeSpan.FromMicroseconds(1));
        answer.UpdatedAt.ShouldBeNull();
        answer.Version.ShouldBe(created.Version);
    }

    [Fact]
    public async Task Score_sums_the_votes_and_IsAccepted_follows_the_question()
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        var created = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader);
        var now = DateTimeOffset.UtcNow;

        // No vote or accept endpoints yet.
        await fixture.WithDbAsync(async db =>
        {
            var loaded = await db.Questions.Include(q => q.Answers).SingleAsync(q => q.Id == question.Id);
            var target = loaded.Answers.Single();

            loaded.AcceptAnswer(target, TestUsers.Author.Id);
            db.AnswerVotes.Add(AnswerVote.Cast(target, TestUsers.Author.Id, VoteValue.Up, now));
            db.AnswerVotes.Add(AnswerVote.Cast(target, TestUsers.Admin.Id, VoteValue.Up, now));
            await db.SaveChangesAsync();
        });

        var answer = await fixture.GetAnswerAsync(created.Id);

        answer.Score.ShouldBe(2);
        answer.IsAccepted.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0199a000-0000-7000-8000-000000000000")]
    [InlineData("abc")]
    public async Task An_unknown_or_malformed_id_is_404(string id)
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/answers/{id}");
            s.StatusCodeShouldBe(404);
        });
    }
}
