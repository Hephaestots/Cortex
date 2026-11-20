using Cortex.QuestionService.Common;

namespace Cortex.QuestionService.Domain;

public sealed class Answer : IVersioned
{
    public const int MaxBodyLength = 5000;

    private readonly List<AnswerVote> _votes = [];
    
    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Body { get; private set; } = null!;
    public string AuthorId { get; private set; } = null!;
    public string AuthorDisplayName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Set by the database on every update (Postgres <c>xmin</c>); see <see cref="IVersioned"/>.</summary>
    public uint Version { get; private set; }

    public Question Question { get; private set; } = null!;
    public IReadOnlyCollection<AnswerVote> Votes => _votes;
    
    private Answer() { }

    internal Answer(Guid questionId, string body, string authorId, string authorDisplayName, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        QuestionId = DomainGuard.AgainstEmpty(questionId, Errors.EmptyQuestionId);
        CreatedAt = now;
        SetBody(body);
        
        DomainGuard.AgainstNullOrWhiteSpace(authorId, Errors.EmptyAuthorId);
        DomainGuard.AgainstNullOrWhiteSpace(authorDisplayName, Errors.EmptyAuthorDisplayName);
        
        if (authorId.Length > UserRules.MaxIdLength)
            throw new DomainException(Errors.IdTooLong);

        if (authorDisplayName.Length > UserRules.MaxDisplayNameLength)
            throw new DomainException(Errors.DisplayNameTooLong);
            
        AuthorId = authorId;
        AuthorDisplayName = authorDisplayName;
    }

    public void Edit(string body, string editorId, DateTimeOffset now)
    {
        DomainGuard.AgainstNullOrWhiteSpace(editorId, Errors.EmptyUserId);
        
        if (editorId != AuthorId)
            throw new DomainException(Errors.InvalidAuthor);
        
        SetBody(body);
        
        UpdatedAt = now;
    }

    private void SetBody(string body)
    {
        DomainGuard.AgainstNullOrWhiteSpace(body, Errors.EmptyBody);
        
        if (body.Length > MaxBodyLength)
            throw new DomainException(Errors.BodyTooLong);
        
        Body = body;
    }

    internal static class Errors
    {
        public static readonly DomainError BodyTooLong =
            new("answer.body_too_long", $"The answer body exceeds {MaxBodyLength} characters.");
        
        public static readonly DomainError IdTooLong =
            new("answer.id_too_long", $"The author id exceeds {UserRules.MaxIdLength} characters.");
        
        public static readonly DomainError DisplayNameTooLong =
            new("answer.display_name_too_long", $"The author display name exceeds {UserRules.MaxDisplayNameLength} characters.");
        
        public static readonly DomainError EmptyAuthorId =
            new("answer.empty_author_id", "The author id must not be null or empty.");
        
        public static readonly DomainError EmptyAuthorDisplayName =
            new("answer.empty_author_display_name", "The author display name must not be null or empty.");
        
        public static readonly DomainError EmptyUserId =
            new("answer.empty_user_id", "The user id must not be null or empty.");
        
        public static readonly DomainError EmptyQuestionId =
            new("answer.empty_question_id", "The question id must not be empty.");
        
        public static readonly DomainError InvalidAuthor = 
            new("answer.invalid_author", "Only the author can edit this answer.");
        
        public static readonly DomainError EmptyBody = 
            new("answer.empty_body", "The body of the answer must not be null or empty");
    }
}