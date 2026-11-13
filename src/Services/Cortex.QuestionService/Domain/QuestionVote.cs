using Cortex.QuestionService.Common;

namespace Cortex.QuestionService.Domain;

public sealed class QuestionVote
{
    public Guid QuestionId { get; private set; }
    public string VoterId { get; private set; } = null!;
    public VoteValue Value { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Question Question { get; private set; } = null!;

    private QuestionVote() { }

    private QuestionVote(Guid questionId, string voterId, VoteValue value, DateTimeOffset now) =>
        (QuestionId, VoterId, Value, CreatedAt) = (questionId, voterId, value, now);

    public static QuestionVote Cast(Question question, string userId, VoteValue value, DateTimeOffset now)
    {
        DomainGuard.AgainstNull(question, Errors.NullQuestion);
        DomainGuard.AgainstNullOrWhiteSpace(userId, Errors.EmptyUserId);

        if (userId.Length > UserIdRules.MaxLength)
            throw new DomainException(Errors.UserIdTooLong);

        if (question.AuthorId == userId)
            throw new DomainException(Errors.OwnQuestion);

        return new QuestionVote(question.Id, userId, EnsureDefined(value), now);
    }

    public void Change(VoteValue value) => Value = EnsureDefined(value);

    // The database has CHECK (value IN (-1, 1)); this turns a bad value into a
    // DomainException instead of a failed save.
    private static VoteValue EnsureDefined(VoteValue value) =>
        Enum.IsDefined(value) ? value : throw new DomainException(Errors.InvalidValue);

    private static class Errors
    {
        public static readonly DomainError NullQuestion =
            new("question_vote.null_question", "The question must not be null.");

        public static readonly DomainError EmptyUserId =
            new("question_vote.empty_user_id", "The user id must not be null or empty.");

        public static readonly DomainError UserIdTooLong =
            new("question_vote.user_id_too_long", $"The user id exceeds {UserIdRules.MaxLength} characters.");

        public static readonly DomainError OwnQuestion =
            new("question_vote.own_question", "You cannot vote on your own question.");

        public static readonly DomainError InvalidValue =
            new("question_vote.invalid_value", "A vote must be up or down.");
    }
}
