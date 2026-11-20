using Cortex.QuestionService.Domain;
using static Cortex.QuestionService.Tests.Domain.DomainBuilders;

namespace Cortex.QuestionService.Tests.Domain;

public sealed class AnswerTests
{
    [Fact]
    public void A_new_answer_keeps_its_fields_and_gets_a_version_7_id()
    {
        var answer = NewQuestion().AddAnswer("Read it again.", OtherUserId, "answerer", Now);

        answer.Id.Version.ShouldBe(7);
        answer.Body.ShouldBe("Read it again.");
        answer.AuthorId.ShouldBe(OtherUserId);
        answer.AuthorDisplayName.ShouldBe("answerer");
        answer.CreatedAt.ShouldBe(Now);
        answer.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Body_and_author_limits_are_enforced()
    {
        var question = NewQuestion();

        ErrorCodeOf(() => question.AddAnswer(" ", OtherUserId, "answerer", Now)).ShouldBe("answer.empty_body");
        ErrorCodeOf(() => question.AddAnswer(new string('b', Answer.MaxBodyLength + 1), OtherUserId, "answerer", Now))
            .ShouldBe("answer.body_too_long");
        ErrorCodeOf(() => question.AddAnswer("Body.", "", "answerer", Now)).ShouldBe("answer.empty_author_id");
        ErrorCodeOf(() => question.AddAnswer("Body.", new string('a', UserRules.MaxIdLength + 1), "answerer", Now))
            .ShouldBe("answer.id_too_long");
        ErrorCodeOf(() => question.AddAnswer("Body.", OtherUserId, "", Now)).ShouldBe("answer.empty_author_display_name");
        ErrorCodeOf(() => question.AddAnswer("Body.", OtherUserId, new string('n', UserRules.MaxDisplayNameLength + 1), Now))
            .ShouldBe("answer.display_name_too_long");
    }

    [Fact]
    public void A_rejected_answer_is_not_added_to_the_question()
    {
        var question = NewQuestion();

        Should.Throw<Common.DomainException>(() => question.AddAnswer("", OtherUserId, "answerer", Now));

        question.Answers.ShouldBeEmpty();
    }

    [Fact]
    public void Edit_replaces_the_body_and_sets_UpdatedAt()
    {
        var answer = NewAnswer(NewQuestion());
        var later = Now.AddMinutes(5);

        answer.Edit("Second thoughts.", later);

        answer.Body.ShouldBe("Second thoughts.");
        answer.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void Edit_enforces_the_body_rules_and_keeps_the_old_body()
    {
        var answer = NewAnswer(NewQuestion());

        ErrorCodeOf(() => answer.Edit(" ", Now)).ShouldBe("answer.empty_body");
        ErrorCodeOf(() => answer.Edit(new string('b', Answer.MaxBodyLength + 1), Now)).ShouldBe("answer.body_too_long");
        answer.Body.ShouldBe("Read it again.");
        answer.UpdatedAt.ShouldBeNull();
    }

    /// <summary>
    /// Same rule as questions (decided 2026-11-20): the author or an admin. The question's
    /// author has no say over other people's answers.
    /// </summary>
    [Theory]
    [InlineData(OtherUserId, false, true)]  // the answer's author
    [InlineData(AuthorId, false, false)]    // the question's author
    [InlineData(AuthorId, true, true)]      // an admin
    public void Only_the_answers_author_or_a_moderator_can_edit(string userId, bool isModerator, bool expected) =>
        NewAnswer(NewQuestion(), authorId: OtherUserId).CanBeEditedBy(userId, isModerator).ShouldBe(expected);
}
