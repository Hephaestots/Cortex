using Cortex.QuestionService.Domain;
using static Cortex.QuestionService.Tests.Domain.DomainBuilders;

namespace Cortex.QuestionService.Tests.Domain;

public sealed class VoteTests
{
    [Fact]
    public void A_question_vote_records_the_voter_and_the_value()
    {
        var question = NewQuestion();

        var vote = QuestionVote.Cast(question, OtherUserId, VoteValue.Up, Now);

        vote.QuestionId.ShouldBe(question.Id);
        vote.VoterId.ShouldBe(OtherUserId);
        vote.Value.ShouldBe(VoteValue.Up);
        vote.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Nobody_votes_on_their_own_question_or_answer()
    {
        var question = NewQuestion(authorId: AuthorId);
        var answer = NewAnswer(question, authorId: OtherUserId);

        ErrorCodeOf(() => QuestionVote.Cast(question, AuthorId, VoteValue.Up, Now)).ShouldBe("question_vote.own_question");
        ErrorCodeOf(() => AnswerVote.Cast(answer, OtherUserId, VoteValue.Up, Now)).ShouldBe("answer_vote.own_answer");
    }

    [Fact]
    public void The_question_author_may_vote_on_an_answer()
    {
        var question = NewQuestion(authorId: AuthorId);
        var answer = NewAnswer(question, authorId: OtherUserId);

        var vote = AnswerVote.Cast(answer, AuthorId, VoteValue.Down, Now);

        vote.AnswerId.ShouldBe(answer.Id);
        vote.Value.ShouldBe(VoteValue.Down);
    }

    /// <summary>
    /// The column has <c>CHECK (value IN (-1, 1))</c>; the domain rejects anything else first, so
    /// a bad value is a 400 with a code instead of a failed save.
    /// </summary>
    [Fact]
    public void Only_up_and_down_are_valid_values()
    {
        var question = NewQuestion();
        var answer = NewAnswer(question);
        const VoteValue zero = 0;
        const VoteValue two = (VoteValue)2;

        ErrorCodeOf(() => QuestionVote.Cast(question, OtherUserId, zero, Now)).ShouldBe("question_vote.invalid_value");
        ErrorCodeOf(() => AnswerVote.Cast(answer, AuthorId, two, Now)).ShouldBe("answer_vote.invalid_value");

        var vote = QuestionVote.Cast(question, OtherUserId, VoteValue.Up, Now);
        ErrorCodeOf(() => vote.Change(zero)).ShouldBe("question_vote.invalid_value");
        vote.Value.ShouldBe(VoteValue.Up);

        vote.Change(VoteValue.Down);
        vote.Value.ShouldBe(VoteValue.Down);
    }

    [Fact]
    public void The_voter_id_is_required_and_limited()
    {
        var question = NewQuestion();
        var tooLong = new string('v', UserRules.MaxIdLength + 1);

        ErrorCodeOf(() => QuestionVote.Cast(question, "", VoteValue.Up, Now)).ShouldBe("question_vote.empty_user_id");
        ErrorCodeOf(() => QuestionVote.Cast(question, tooLong, VoteValue.Up, Now)).ShouldBe("question_vote.user_id_too_long");
    }
}
