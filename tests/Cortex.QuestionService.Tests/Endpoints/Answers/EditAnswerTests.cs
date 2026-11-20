using Cortex.QuestionService.Features.Answers;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Answers;

/// <summary>Status order (see the endpoint): 401, 400 from the validator, 404, 403, 409.</summary>
public sealed class EditAnswerTests(AppFixture fixture)
{
    private async Task<CreateAnswerResponse> NewAnswerAsync(TestUser? author = null)
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        return await fixture.CreateAnswerAsync(question.Id, author ?? TestUsers.Reader);
    }

    private static EditAnswerRequest EditOf(CreateAnswerResponse answer) => new("Edited answer.", answer.Version);

    private Task<IScenarioResult> PutAsync(Guid id, EditAnswerRequest request, TestUser? user, int expectedStatus) =>
        fixture.Host.Scenario(s =>
        {
            if (user is not null)
                s.As(user);

            s.Put.Json(request).ToUrl($"/api/answers/{id}");
            s.StatusCodeShouldBe(expectedStatus);
        });

    [Fact]
    public async Task Without_a_user_is_401()
    {
        var answer = await NewAnswerAsync();

        await PutAsync(answer.Id, EditOf(answer), user: null, 401);
    }

    [Fact]
    public async Task The_author_replaces_the_body_and_gets_204()
    {
        var answer = await NewAnswerAsync(TestUsers.Reader);

        await PutAsync(answer.Id, EditOf(answer), TestUsers.Reader, 204);

        var stored = await fixture.GetAnswerAsync(answer.Id);
        stored.Body.ShouldBe("Edited answer.");
        stored.UpdatedAt.ShouldNotBeNull();
        stored.Version.ShouldNotBe(answer.Version);
        stored.AuthorId.ShouldBe(TestUsers.Reader.Id);
    }

    [Fact]
    public async Task An_admin_may_edit_someone_elses_answer()
    {
        var answer = await NewAnswerAsync(TestUsers.Reader);

        await PutAsync(answer.Id, EditOf(answer), TestUsers.Admin, 204);

        (await fixture.GetAnswerAsync(answer.Id)).Body.ShouldBe("Edited answer.");
    }

    [Fact]
    public async Task The_questions_author_is_403_with_the_edit_forbidden_code()
    {
        // TestUsers.Author asked the question; the answer is the reader's.
        var answer = await NewAnswerAsync(TestUsers.Reader);

        var result = await PutAsync(answer.Id, EditOf(answer), TestUsers.Author, 403);

        (await result.ReadProblemAsync()).Code.ShouldBe("answer.edit_forbidden");
        (await fixture.GetAnswerAsync(answer.Id)).Body.ShouldBe(answer.Body);
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        var answer = await NewAnswerAsync();

        await PutAsync(Guid.CreateVersion7(), EditOf(answer), TestUsers.Reader, 404);
    }

    [Fact]
    public async Task A_missing_version_is_400_under_Version()
    {
        var answer = await NewAnswerAsync();

        var result = await PutAsync(answer.Id, EditOf(answer) with { Version = 0 }, TestUsers.Reader, 400);

        (await result.ReadProblemAsync()).ErrorsFor("Version").ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(0, "The body of the answer must not be null or empty.")]
    [InlineData(5001, "The answer body exceeds 5000 characters.")]
    public async Task An_empty_or_too_long_body_is_400_under_Body(int length, string message)
    {
        var answer = await NewAnswerAsync();
        var request = EditOf(answer) with { Body = new string('a', length) };

        var result = await PutAsync(answer.Id, request, TestUsers.Reader, 400);

        (await result.ReadProblemAsync()).ErrorsFor("Body").ShouldBe([message]);
    }

    [Fact]
    public async Task An_invalid_body_is_400_before_the_404_and_the_403()
    {
        var answer = await NewAnswerAsync(TestUsers.Reader);
        var invalid = EditOf(answer) with { Body = "" };

        await PutAsync(Guid.CreateVersion7(), invalid, TestUsers.Reader, 400);
        await PutAsync(answer.Id, invalid, TestUsers.Author, 400);
    }

    [Fact]
    public async Task An_edit_from_an_old_version_is_409_and_the_newer_edit_is_kept()
    {
        var answer = await NewAnswerAsync(TestUsers.Reader);
        await PutAsync(answer.Id, EditOf(answer), TestUsers.Reader, 204);

        var result = await PutAsync(answer.Id, EditOf(answer) with { Body = "Second." }, TestUsers.Reader, 409);

        (await result.ReadProblemAsync()).Code.ShouldBe("concurrency.stale_version");
        (await fixture.GetAnswerAsync(answer.Id)).Body.ShouldBe("Edited answer.");
    }

    [Fact]
    public async Task Permission_is_checked_before_the_version()
    {
        var answer = await NewAnswerAsync(TestUsers.Reader);
        await PutAsync(answer.Id, EditOf(answer), TestUsers.Reader, 204);

        await PutAsync(answer.Id, EditOf(answer), TestUsers.Author, 403);
    }

    [Fact]
    public async Task Editing_an_answer_leaves_the_questions_version_alone()
    {
        var question = await fixture.CreateQuestionAsync(author: TestUsers.Author);
        var answer = await fixture.CreateAnswerAsync(question.Id, TestUsers.Reader);

        await PutAsync(answer.Id, EditOf(answer), TestUsers.Reader, 204);

        (await fixture.GetQuestionAsync(question.Id)).Version.ShouldBe(question.Version);
    }
}
