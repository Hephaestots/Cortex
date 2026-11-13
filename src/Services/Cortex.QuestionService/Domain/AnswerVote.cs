using Cortex.QuestionService.Common;

namespace Cortex.QuestionService.Domain;

public sealed class AnswerVote
{
    public Guid AnswerId { get; private set; }
    public string VoterId { get; private set; } = null!;
    public VoteValue Value { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Answer Answer { get; private set; } = null!;

    private AnswerVote() { }

    private AnswerVote(Guid answerId, string voterId, VoteValue value, DateTimeOffset now) =>
        (AnswerId, VoterId, Value, CreatedAt) = (answerId, voterId, value, now);

    public static AnswerVote Cast(Answer answer, string userId, VoteValue value, DateTimeOffset now)
    {
        DomainGuard.AgainstNull(answer, Errors.NullAnswer);
        DomainGuard.AgainstNullOrWhiteSpace(userId, Errors.EmptyUserId);

        if (userId.Length > UserIdRules.MaxLength)
            throw new DomainException(Errors.UserIdTooLong);

        if (answer.AuthorId == userId)
            throw new DomainException(Errors.OwnAnswer);

        return new AnswerVote(answer.Id, userId, EnsureDefined(value), now);
    }

    public void Change(VoteValue value) => Value = EnsureDefined(value);

    // The database has CHECK (value IN (-1, 1)); this turns a bad value into a
    // DomainException instead of a failed save.
    private static VoteValue EnsureDefined(VoteValue value) =>
        Enum.IsDefined(value) ? value : throw new DomainException(Errors.InvalidValue);

    private static class Errors
    {
        public static readonly DomainError NullAnswer =
            new("answer_vote.null_answer", "The answer must not be null.");

        public static readonly DomainError EmptyUserId =
            new("answer_vote.empty_user_id", "The user id must not be null or empty.");

        public static readonly DomainError UserIdTooLong =
            new("answer_vote.user_id_too_long", $"The user id exceeds {UserIdRules.MaxLength} characters.");

        public static readonly DomainError OwnAnswer =
            new("answer_vote.own_answer", "You cannot vote on your own answer.");

        public static readonly DomainError InvalidValue =
            new("answer_vote.invalid_value", "A vote must be up or down.");
    }
}
