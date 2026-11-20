using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Questions;

/// <summary>
/// Status order (see the endpoint): 401, 400 from the validator, 404, 403, 409, then the
/// unknown-tag 400.
/// </summary>
public sealed class EditQuestionTests(AppFixture fixture)
{
    private static EditQuestionRequest EditOf(CreateQuestionResponse question) =>
        new("  Edited title  ", "Edited body.", ["fantasy", "poetry"], question.Version);

    private Task<IScenarioResult> PutAsync(Guid id, object request, TestUser? user, int expectedStatus) =>
        fixture.Host.Scenario(s =>
        {
            if (user is not null)
                s.As(user);

            s.Put.Json(request).ToUrl($"/api/questions/{id}");
            s.StatusCodeShouldBe(expectedStatus);
        });

    [Fact]
    public async Task Without_a_user_is_401()
    {
        var question = await fixture.CreateQuestionAsync();

        await PutAsync(question.Id, EditOf(question), user: null, 401);
    }

    [Fact]
    public async Task The_author_replaces_title_body_and_tags_and_gets_204()
    {
        var question = await fixture.CreateQuestionAsync(tags: ["horror"]);

        await PutAsync(question.Id, EditOf(question), TestUsers.Author, 204);

        var stored = await fixture.GetQuestionAsync(question.Id);
        stored.Title.ShouldBe("Edited title"); // trimmed by the domain
        stored.Body.ShouldBe("Edited body.");
        stored.Tags.ShouldBe(["fantasy", "poetry"]);
        stored.UpdatedAt.ShouldNotBeNull();
        stored.Version.ShouldNotBe(question.Version);
    }

    [Fact]
    public async Task An_admin_may_edit_someone_elses_question_but_not_its_book()
    {
        var question = await fixture.CreateQuestionAsync(bookId: "OL3W");

        // The request type has no BookId; a client sending one anyway is ignored.
        var withBook = new { title = "By the admin", body = "Body.", tagSlugs = new[] { "fantasy" }, bookId = "OL9W", version = question.Version };

        await PutAsync(question.Id, withBook, TestUsers.Admin, 204);

        var stored = await fixture.GetQuestionAsync(question.Id);
        stored.Title.ShouldBe("By the admin");
        stored.BookId.ShouldBe("OL3W");
        stored.AuthorId.ShouldBe(TestUsers.Author.Id);
    }

    [Fact]
    public async Task Another_user_is_403_with_the_edit_forbidden_code()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await PutAsync(question.Id, EditOf(question), TestUsers.Reader, 403);

        (await result.ReadProblemAsync()).Code.ShouldBe("question.edit_forbidden");
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        var question = await fixture.CreateQuestionAsync();

        await PutAsync(Guid.CreateVersion7(), EditOf(question), TestUsers.Author, 404);
    }

    [Fact]
    public async Task A_missing_version_is_400_under_Version()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await PutAsync(question.Id, EditOf(question) with { Version = 0 }, TestUsers.Author, 400);

        (await result.ReadProblemAsync()).ErrorsFor("Version").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Six_tags_are_400_under_TagSlugs()
    {
        var question = await fixture.CreateQuestionAsync();
        var request = EditOf(question) with { TagSlugs = ["fantasy", "horror", "poetry", "mystery", "romance", "classics"] };

        var result = await PutAsync(question.Id, request, TestUsers.Author, 400);

        (await result.ReadProblemAsync()).ErrorsFor("TagSlugs").ShouldBe(["A question needs between 1 and 5 tags."]);
    }

    [Fact]
    public async Task An_unknown_tag_is_400_under_TagSlugs()
    {
        var question = await fixture.CreateQuestionAsync();

        var result = await PutAsync(question.Id, EditOf(question) with { TagSlugs = ["nope"] }, TestUsers.Author, 400);

        (await result.ReadProblemAsync()).ErrorsFor("TagSlugs").ShouldBe(["Tag 'nope' does not exist."]);
    }

    [Fact]
    public async Task An_invalid_body_is_400_before_the_404_and_the_403()
    {
        var question = await fixture.CreateQuestionAsync();
        var invalid = EditOf(question) with { Title = "" };

        // Validation runs before LoadAsync: the missing question and the missing permission never get checked.
        await PutAsync(Guid.CreateVersion7(), invalid, TestUsers.Author, 400);
        await PutAsync(question.Id, invalid, TestUsers.Reader, 400);
    }

    [Fact]
    public async Task An_edit_from_an_old_version_is_409_after_someone_else_saved()
    {
        var question = await fixture.CreateQuestionAsync();
        await PutAsync(question.Id, EditOf(question), TestUsers.Author, 204);

        // Same version as the first edit, which is now stale.
        var result = await PutAsync(question.Id, EditOf(question) with { Title = "Second" }, TestUsers.Author, 409);

        (await result.ReadProblemAsync()).Code.ShouldBe("concurrency.stale_version");
        (await fixture.GetQuestionAsync(question.Id)).Title.ShouldBe("Edited title");
    }

    [Fact]
    public async Task Permission_is_checked_before_the_version()
    {
        var question = await fixture.CreateQuestionAsync();
        await PutAsync(question.Id, EditOf(question), TestUsers.Author, 204);

        await PutAsync(question.Id, EditOf(question), TestUsers.Reader, 403);
    }

    /// <summary>
    /// The second of the two concurrency checks: a write that lands between the endpoint's
    /// LoadAsync and its SaveChangesAsync. The version the client sent is current, so Validate
    /// passes, but EF's <c>UPDATE ... AND xmin = @version</c> matches no row and
    /// <c>ConcurrencyExceptionHandler</c> answers 409.
    /// </summary>
    /// <remarks>
    /// Forced with a row lock: another transaction updates the row and holds the lock, the PUT
    /// loads the old version (reads are not blocked) and waits on its UPDATE; once Postgres reports
    /// it waiting, the other transaction commits, which changes xmin under the waiting UPDATE.
    /// </remarks>
    [Fact]
    public async Task A_write_between_load_and_save_is_409_and_the_other_write_is_kept()
    {
        var question = await fixture.CreateQuestionAsync();
        var ct = TestContext.Current.CancellationToken;

        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.QuestionDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE questions SET body = 'Saved by the other writer.' WHERE question_id = {question.Id}", ct);

        var put = PutAsync(question.Id, EditOf(question), TestUsers.Author, 409);

        await WaitUntilAnUpdateIsBlockedOnALockAsync();
        await transaction.CommitAsync(ct);

        var result = await put;
        (await result.ReadProblemAsync()).Code.ShouldBe("concurrency.stale_version");
        (await fixture.GetQuestionAsync(question.Id)).Body.ShouldBe("Saved by the other writer.");
    }

    private async Task WaitUntilAnUpdateIsBlockedOnALockAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            var waiting = await fixture.WithDbAsync(db => db.Database
                .SqlQuery<int>($"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock' AND query LIKE 'UPDATE questions%'
                    """)
                .SingleAsync());

            if (waiting > 0)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("The PUT never blocked on the row lock.");
    }
}
