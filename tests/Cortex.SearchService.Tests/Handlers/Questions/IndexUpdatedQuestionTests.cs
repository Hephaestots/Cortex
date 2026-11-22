using Cortex.SearchService.Tests.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Tests.Handlers.Questions;

public sealed class IndexUpdatedQuestionTests(AppFixture fixture)
{
    [Fact]
    public async Task An_updated_question_is_indexed_with_only_updatable_fields()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);

        var updated = TestData.QuestionUpdated(created.QuestionId) with
        {
            Body = "# Heading\n\nSee **this** [link](https://example.com) and `code`."
        };

        await fixture.SendAsync(updated);

        var document = await fixture.GetQuestionDocumentAsync(updated.QuestionId);
        // What an edit owns: the new values, the body as plain text.
        document.Title.ShouldBe(updated.Title);
        document.Body.ShouldBe("Heading See this link and code.");
        document.Tags.ShouldBe(["poetry"]);
        document.LastActivityAt.ShouldBe(updated.UpdatedAt.ToUnixTimeSeconds());
        document.Version.ShouldBe(updated.Version);
        // What it doesn't: still the created values.
        document.AuthorId.ShouldBe(created.AuthorId);
        document.AuthorDisplayName.ShouldBe(created.AuthorDisplayName);
        document.BookId.ShouldBe(created.BookId);
        document.CreatedAt.ShouldBe(created.CreatedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task An_updated_question_keeps_its_score_answers_and_accepted_answer()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);

        // Non-default values, or a reset to 0 and false would pass unnoticed.
        await fixture.SetCountsAsync(created.QuestionId, score: 7, answerCount: 3, hasAcceptedAnswer: true);

        await fixture.SendAsync(TestData.QuestionUpdated(created.QuestionId));

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.Score.ShouldBe(7);
        document.AnswerCount.ShouldBe(3);
        document.HasAcceptedAnswer.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public async Task An_update_with_an_equal_or_older_version_is_skipped(uint versionsBehind)
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);
        var before = await fixture.GetQuestionDocumentAsync(created.QuestionId);

        var updated = TestData.QuestionUpdated(created.QuestionId) with
        {
            Version = created.Version - versionsBehind
        };

        await fixture.SendAsync(updated);

        // Every field as it was, compared with the document itself (not the event, whose body is Markdown).
        var after = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        after.ShouldBeEquivalentTo(before);
    }

    [Fact]
    public async Task The_same_update_delivered_twice_is_applied_once()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);

        var updated = TestData.QuestionUpdated(created.QuestionId);
        await fixture.SendAsync(updated);
        var afterFirst = await fixture.GetQuestionDocumentAsync(created.QuestionId);

        // RabbitMQ delivers at least once: the second copy has the indexed version, so it is skipped.
        await fixture.SendAsync(updated);

        var afterSecond = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        afterSecond.ShouldBeEquivalentTo(afterFirst);
        afterSecond.Version.ShouldBe(updated.Version);
    }

    [Fact]
    public async Task An_update_of_a_missing_question_throws_not_found()
    {
        // In production the error policy then moves the message to the dead-letter queue, to be
        // replayed after its create. Invoking directly skips that step and lets the exception out.
        var updated = TestData.QuestionUpdated(Guid.CreateVersion7());

        await Should.ThrowAsync<TypesenseApiNotFoundException>(() => fixture.SendAsync(updated));
    }
}
