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
    public void The_author_edits_the_body_and_UpdatedAt_is_set()
    {
        var answer = NewAnswer(NewQuestion());
        var later = Now.AddMinutes(5);

        answer.Edit("Second thoughts.", OtherUserId, later);

        answer.Body.ShouldBe("Second thoughts.");
        answer.UpdatedAt.ShouldBe(later);
    }

    /// <summary>
    /// Unlike <c>Question</c>, where the endpoint asks <c>CanBeEditedBy</c> and admins may edit,
    /// <c>Answer.Edit</c> checks the editor itself and allows only the author. Pinned here so the
    /// UpdateAnswer slice changes it on purpose if admins should edit answers too.
    /// </summary>
    [Fact]
    public void Only_the_author_can_edit_an_answer()
    {
        var answer = NewAnswer(NewQuestion());

        ErrorCodeOf(() => answer.Edit("Hijacked.", AuthorId, Now)).ShouldBe("answer.invalid_author");
        answer.Body.ShouldBe("Read it again.");
    }
}
