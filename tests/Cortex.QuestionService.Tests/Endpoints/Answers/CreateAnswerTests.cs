using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Answers;

/// <summary>Status order (see the endpoint): 401, 400 from the validator, 404.</summary>
public sealed class CreateAnswerTests(AppFixture fixture)
{
    private Task<IScenarioResult> PostAsync(string questionId, object request, TestUser? user, int expectedStatus) =>
        fixture.Host.Scenario(s =>
        {
            if (user is not null)
                s.As(user);

            s.Post.Json(request).ToUrl($"/api/questions/{questionId}/answers");
            s.StatusCodeShouldBe(expectedStatus);
        });

    private static readonly CreateAnswerRequest ValidRequest = new("It reads differently once you know the ending.");

    [Fact]
    public async Task Without_a_user_is_401()
    {
        var question = await fixture.CreateQuestionAsync();

        await PostAsync(question.Id.ToString(), ValidRequest, user: null, 401);
    }

    [Fact]
    public async Task Creates_the_answer_for_the_caller_and_returns_201_with_its_location()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await PostAsync(question.Id.ToString(), ValidRequest, TestUsers.Reader, 201);
        var answer = await result.ReadAsJsonAsync<CreateAnswerResponse>();

        answer.ShouldNotBeNull();
        result.Context.Response.Headers.Location.ToString().ShouldBe($"/api/answers/{answer.Id}");
        answer.QuestionId.ShouldBe(question.Id);
        answer.Body.ShouldBe(ValidRequest.Body);
        answer.AuthorId.ShouldBe(TestUsers.Reader.Id);
        answer.AuthorDisplayName.ShouldBe(TestUsers.Reader.UserName);
        // Read back from the INSERT, for the future edit endpoint.
        answer.Version.ShouldNotBe(0u);

        // The row really is there: the insert path that ValueGeneratedNever() fixed.
        var stored = await fixture.WithDbAsync(db => db.Answers.SingleAsync(a => a.Id == answer.Id));
        stored.QuestionId.ShouldBe(question.Id);
        stored.AuthorDisplayName.ShouldBe(TestUsers.Reader.UserName);
        stored.Version.ShouldBe(answer.Version);
    }

    [Fact]
    public async Task The_question_author_may_answer_their_own_question()
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);

        await PostAsync(question.Id.ToString(), ValidRequest, TestUsers.Author, 201);
    }

    [Fact]
    public async Task Answers_are_counted_on_the_question_and_leave_its_version_alone()
    {
        var question = await fixture.CreateQuestionAsync();

        await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader);
        await fixture.CreateAnswerAsync(question.Id, TestUsers.Admin);

        // Adding an answer is an INSERT into answers; the question row is never written, which
        // is why the request carries no version (see "CreateAnswer" in docs/status.md).
        var stored = await fixture.GetQuestionAsync(question.Id);
        stored.AnswersCount.ShouldBe(2);
        stored.Version.ShouldBe(question.Version);
    }

    [Fact]
    public async Task An_unknown_question_is_404()
    {
        await PostAsync(Guid.CreateVersion7().ToString(), ValidRequest, TestUsers.Reader, 404);
    }

    [Fact]
    public async Task A_question_id_that_is_not_a_guid_is_404()
    {
        await PostAsync("abc", ValidRequest, TestUsers.Reader, 404);
    }

    [Fact]
    public async Task An_empty_body_is_400_under_Body()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await PostAsync(question.Id.ToString(), new CreateAnswerRequest(""), TestUsers.Reader, 400);

        (await result.ReadProblemAsync()).ErrorsFor("Body")
            .ShouldBe(["The body of the answer must not be null or empty"]);
    }

    [Fact]
    public async Task A_body_over_5000_characters_is_400_under_Body()
    {
        var question = await fixture.CreateQuestionAsync();
        var tooLong = new CreateAnswerRequest(new string('a', 5001));

        var result = await PostAsync(question.Id.ToString(), tooLong, TestUsers.Reader, 400);

        (await result.ReadProblemAsync()).ErrorsFor("Body")
            .ShouldBe(["The answer body exceeds 5000 characters."]);
    }

    [Fact]
    public async Task An_invalid_body_is_400_before_the_404()
    {
        await PostAsync(Guid.CreateVersion7().ToString(), new CreateAnswerRequest(""), TestUsers.Reader, 400);
    }

    [Fact]
    public async Task The_old_singular_route_does_not_exist()
    {
        var question = await fixture.CreateQuestionAsync();

        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Post.Json(ValidRequest).ToUrl($"/api/questions/{question.Id}/answer");
            s.StatusCodeShouldBe(404);
        });
    }
}
