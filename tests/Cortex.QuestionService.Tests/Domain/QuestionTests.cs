using Cortex.QuestionService.Domain;
using static Cortex.QuestionService.Tests.Domain.DomainBuilders;

namespace Cortex.QuestionService.Tests.Domain;

public sealed class QuestionTests
{
    [Fact]
    public void A_new_question_keeps_its_fields_and_gets_a_version_7_id()
    {
        var question = NewQuestion(bookId: "OL1W");

        question.Id.Version.ShouldBe(7);
        question.Title.ShouldBe("Is the narrator reliable?");
        question.AuthorId.ShouldBe(AuthorId);
        question.BookId.ShouldBe("OL1W");
        question.CreatedAt.ShouldBe(Now);
        question.UpdatedAt.ShouldBeNull();
        question.HasAcceptedAnswer.ShouldBeFalse();
    }

    [Fact]
    public void A_new_question_comes_with_its_view_counter_at_zero()
    {
        var question = NewQuestion();

        question.ViewCount.QuestionId.ShouldBe(question.Id);
        question.ViewCount.Count.ShouldBe(0);
    }

    [Fact]
    public void The_title_is_trimmed_before_its_length_is_checked()
    {
        var exactlyMax = new string('t', Question.MaxTitleLength);

        var question = NewQuestion(title: $"   {exactlyMax}   ");

        question.Title.ShouldBe(exactlyMax);
        ErrorCodeOf(() => NewQuestion(title: exactlyMax + "t")).ShouldBe("question.title_too_long");
    }

    [Theory]
    [InlineData("", "question.empty_title")]
    [InlineData("   ", "question.empty_title")]
    public void A_blank_title_is_rejected(string title, string code) =>
        ErrorCodeOf(() => NewQuestion(title: title)).ShouldBe(code);

    [Fact]
    public void Body_book_id_and_author_limits_are_enforced()
    {
        ErrorCodeOf(() => NewQuestion(body: " ")).ShouldBe("question.empty_body");
        ErrorCodeOf(() => NewQuestion(body: new string('b', Question.MaxBodyLength + 1))).ShouldBe("question.body_too_long");
        ErrorCodeOf(() => NewQuestion(bookId: new string('k', Question.MaxBookIdLength + 1))).ShouldBe("question.book_id_too_long");
        ErrorCodeOf(() => NewQuestion(authorId: "")).ShouldBe("question.empty_author_id");
        ErrorCodeOf(() => NewQuestion(authorId: new string('a', UserRules.MaxIdLength + 1))).ShouldBe("question.author_id_too_long");
        ErrorCodeOf(() => NewQuestion(authorDisplayName: "")).ShouldBe("question.empty_author_display_name");
        ErrorCodeOf(() => NewQuestion(authorDisplayName: new string('n', UserRules.MaxDisplayNameLength + 1)))
            .ShouldBe("question.author_display_name_too_long");
    }

    [Fact]
    public void A_question_needs_between_one_and_five_distinct_tags()
    {
        var tags = Enumerable.Range(1, 6).Select(i => NewTag($"tag-{i}")).ToList();

        ErrorCodeOf(() => NewQuestion(tags: [])).ShouldBe("question.tags_out_of_range");
        ErrorCodeOf(() => NewQuestion(tags: tags)).ShouldBe("question.tags_out_of_range");
        NewQuestion(tags: tags[..5]).Tags.Count.ShouldBe(5);
    }

    [Fact]
    public void The_same_tag_twice_counts_once()
    {
        var tag = NewTag();

        NewQuestion(tags: [tag, tag]).Tags.ShouldHaveSingleItem().ShouldBe(tag);
    }

    [Theory]
    [InlineData(AuthorId, false, true)]
    [InlineData(OtherUserId, false, false)]
    [InlineData(OtherUserId, true, true)]
    public void Only_the_author_or_a_moderator_can_edit(string userId, bool isModerator, bool expected) =>
        NewQuestion().CanBeEditedBy(userId, isModerator).ShouldBe(expected);

    [Fact]
    public void Edit_replaces_title_body_and_tags_and_sets_UpdatedAt()
    {
        var question = NewQuestion(tags: [NewTag("horror")]);
        var later = Now.AddHours(1);

        question.Edit(" New title ", "New body.", [NewTag("poetry"), NewTag("drama")], later);

        question.Title.ShouldBe("New title");
        question.Body.ShouldBe("New body.");
        question.Tags.Select(t => t.Slug).ShouldBe(["poetry", "drama"], ignoreOrder: true);
        question.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Edit_enforces_the_same_rules_as_creating()
    {
        var question = NewQuestion();

        ErrorCodeOf(() => question.Edit("", "Body.", [NewTag()], Now)).ShouldBe("question.empty_title");
        ErrorCodeOf(() => question.Edit("Title", "Body.", [], Now)).ShouldBe("question.tags_out_of_range");
    }

    [Fact]
    public void AddAnswer_links_the_answer_to_the_question()
    {
        var question = NewQuestion();

        var answer = NewAnswer(question);

        answer.QuestionId.ShouldBe(question.Id);
        question.Answers.ShouldHaveSingleItem().ShouldBe(answer);
    }

    [Fact]
    public void Only_the_author_can_accept_an_answer()
    {
        var question = NewQuestion();
        var answer = NewAnswer(question);

        ErrorCodeOf(() => question.AcceptAnswer(answer, OtherUserId)).ShouldBe("question.invalid_user");

        question.AcceptAnswer(answer, AuthorId);

        question.AcceptedAnswerId.ShouldBe(answer.Id);
        question.HasAcceptedAnswer.ShouldBeTrue();
    }

    [Fact]
    public void Only_the_author_can_clear_the_accepted_answer()
    {
        var question = NewQuestion();
        question.AcceptAnswer(NewAnswer(question), AuthorId);

        ErrorCodeOf(() => question.ClearAcceptedAnswer(OtherUserId)).ShouldBe("question.invalid_user");

        question.ClearAcceptedAnswer(AuthorId);

        question.AcceptedAnswerId.ShouldBeNull();
    }

    [Fact]
    public void An_answer_to_another_question_cannot_be_accepted_or_removed()
    {
        var question = NewQuestion();
        var foreign = NewAnswer(NewQuestion());

        ErrorCodeOf(() => question.AcceptAnswer(foreign, AuthorId)).ShouldBe("question.invalid_answer");
        ErrorCodeOf(() => question.RemoveAnswer(foreign)).ShouldBe("question.invalid_answer");
    }

    [Fact]
    public void Removing_the_accepted_answer_clears_it()
    {
        var question = NewQuestion();
        var accepted = NewAnswer(question);
        var other = NewAnswer(question);
        question.AcceptAnswer(accepted, AuthorId);

        question.RemoveAnswer(accepted);

        question.AcceptedAnswerId.ShouldBeNull();
        question.Answers.ShouldHaveSingleItem().ShouldBe(other);
    }
}
