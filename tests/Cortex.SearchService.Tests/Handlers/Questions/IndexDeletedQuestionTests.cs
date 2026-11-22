using Cortex.Contracts.Features.Questions;
using Cortex.SearchService.Tests.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Tests.Handlers.Questions;

public sealed class IndexDeletedQuestionTests(AppFixture fixture)
{
    [Fact]
    public async Task A_deleted_question_is_removed_from_the_index()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);

        await fixture.SendAsync(new QuestionDeleted(created.QuestionId));

        await Should.ThrowAsync<TypesenseApiNotFoundException>(
            () => fixture.GetQuestionDocumentAsync(created.QuestionId));
    }

    [Fact]
    public async Task Deleting_a_question_leaves_the_others_indexed()
    {
        var deleted = TestData.QuestionCreated();
        var kept = TestData.QuestionCreated();
        await fixture.SendAsync(deleted);
        await fixture.SendAsync(kept);

        await fixture.SendAsync(new QuestionDeleted(deleted.QuestionId));

        var document = await fixture.GetQuestionDocumentAsync(kept.QuestionId);
        document.Title.ShouldBe(kept.Title);
    }

    [Fact]
    public async Task The_same_delete_delivered_twice_is_handled_both_times()
    {
        var created = TestData.QuestionCreated();
        await fixture.SendAsync(created);
        var deleted = new QuestionDeleted(created.QuestionId);
        await fixture.SendAsync(deleted);

        // RabbitMQ delivers at least once. The second delete is a 404, which must count as done:
        // otherwise this call throws, and in production the message would end in the dead-letter queue.
        await fixture.SendAsync(deleted);

        await Should.ThrowAsync<TypesenseApiNotFoundException>(
            () => fixture.GetQuestionDocumentAsync(created.QuestionId));
    }

    [Fact]
    public async Task Deleting_a_question_that_was_never_indexed_succeeds()
    {
        // Its create may have been dead-lettered: the question is gone in Postgres either way, so
        // there is nothing for the index to keep. Completing without an exception is the assertion.
        await fixture.SendAsync(new QuestionDeleted(Guid.CreateVersion7()));
    }
}
