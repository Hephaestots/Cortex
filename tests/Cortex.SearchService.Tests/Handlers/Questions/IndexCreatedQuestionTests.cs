using Cortex.SearchService.Features.Questions;
using Cortex.SearchService.Tests.Infrastructure;

namespace Cortex.SearchService.Tests.Handlers.Questions;

public sealed class IndexCreatedQuestionTests(AppFixture fixture)
{
    [Fact]
    public async Task A_created_question_is_indexed_with_every_field()
    {
        var created = TestData.QuestionCreated() with
        {
            Title = "Is the narrator reliable?",
            Body = "# Heading\n\nSee **this** [link](https://example.com) and `code`.",
            TagSlugs = ["fantasy", "science-fiction"]
        };

        await fixture.SendAsync(created);

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.Id.ShouldBe(created.QuestionId.ToString());
        document.Title.ShouldBe("Is the narrator reliable?");
        // The plain text a reader sees: no Markdown syntax, and the link's target dropped.
        document.Body.ShouldBe("Heading See this link and code.");
        document.Tags.ShouldBe(["fantasy", "science-fiction"]);
        document.AuthorId.ShouldBe(created.AuthorId);
        document.AuthorDisplayName.ShouldBe(created.AuthorDisplayName);
        document.BookId.ShouldBe(created.BookId);
        document.CreatedAt.ShouldBe(created.CreatedAt.ToUnixTimeSeconds());
        document.LastActivityAt.ShouldBe(document.CreatedAt);
        document.Version.ShouldBe(created.Version);
    }

    [Fact]
    public async Task A_new_question_starts_with_no_score_answers_or_accepted_answer()
    {
        var created = TestData.QuestionCreated();

        await fixture.SendAsync(created);

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.Score.ShouldBe(0);
        document.AnswerCount.ShouldBe(0);
        document.HasAcceptedAnswer.ShouldBeFalse();
    }

    [Fact]
    public async Task A_question_without_a_book_is_indexed_without_one()
    {
        // book_id is optional in the schema; Typesense would reject a document missing a required field.
        var created = TestData.QuestionCreated() with { BookId = null };

        await fixture.SendAsync(created);

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.BookId.ShouldBeNull();
    }

    [Fact]
    public async Task The_same_create_delivered_twice_is_handled_both_times()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);

        // RabbitMQ delivers at least once. The second create is a 409, which must count as done:
        // otherwise this call throws, and in production the message would end in the dead-letter queue.
        await fixture.SendAsync(created);

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.Title.ShouldBe(created.Title);
        document.Version.ShouldBe(created.Version);
    }

    [Fact]
    public async Task A_create_delivered_again_after_an_edit_keeps_the_edit()
    {
        var created = TestData.QuestionCreated() with { Title = "Original title", Version = 100 };
        await fixture.SendAsync(created);

        // The edit is written straight to Typesense, not through the update handler, so this test
        // fails only for the create handler.
        await fixture.Typesense.UpdateDocument(
            SearchQuestion.CollectionName,
            created.QuestionId.ToString(),
            new SearchQuestionEdit
            {
                Title = "Edited title",
                Body = "Edited body",
                Tags = ["fantasy"],
                LastActivityAt = created.CreatedAt.ToUnixTimeSeconds() + 60,
                Version = 110
            },
            TestContext.Current.CancellationToken);

        await fixture.SendAsync(created);

        var document = await fixture.GetQuestionDocumentAsync(created.QuestionId);
        document.Title.ShouldBe("Edited title");
        document.Body.ShouldBe("Edited body");
        document.Version.ShouldBe(110);
    }
}
