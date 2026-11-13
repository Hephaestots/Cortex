using Cortex.QuestionService.Common;

namespace Cortex.QuestionService.Domain;

public sealed class Question
{
    public const int MaxBodyLength = 5000;
    public const int MaxBookIdLength = 100;
    public const int MaxTags = 5;
    public const int MaxTitleLength = 300;
    public const int MaxDisplayNameLength = 100;

    private readonly List<Answer> _answers = [];
    private readonly List<Tag> _tags = [];
    private readonly List<QuestionVote> _votes = [];
    
    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string AuthorId { get; private set; } = null!;
    public string AuthorDisplayName { get; private set; } = null!;
    public string? BookId { get; private set; }
    public int ViewCount { get; private set; }
    public Guid? AcceptedAnswerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<Answer> Answers => _answers;
    public IReadOnlyCollection<Tag> Tags => _tags;
    public IReadOnlyCollection<QuestionVote> Votes => _votes;
    
    public bool HasAcceptedAnswer => AcceptedAnswerId is not null;
    
    private Question() { }

    public Question(string title, string body, string authorId, string authorDisplayName, 
        string? bookId, IEnumerable<Tag> tags, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        CreatedAt = now;

        if (bookId?.Length > MaxBookIdLength)
            throw new DomainException(Errors.BookIdTooLong);
        
        BookId = bookId;
        
        SetAuthor(authorId,  authorDisplayName);
        SetContent(title, body);
        SetTags(tags);
    }

    public void Edit(string title, string body, IEnumerable<Tag> tags, string editorId, DateTimeOffset now)
    {
        EnsureAuthor(editorId);
        
        SetContent(title, body);
        SetTags(tags);
        
        UpdatedAt = now;
    }

    public Answer AddAnswer(string body, string authorId, DateTimeOffset now)
    {
        var answer = new Answer(Id, body, authorId, now);
        
        _answers.Add(answer);

        return answer;
    }

    public void RemoveAnswer(Answer answer)
    {
        EnsureOwns(answer);
        
        if (AcceptedAnswerId == answer.Id)
            AcceptedAnswerId = null;
        
        _answers.Remove(answer);
    }

    public void AcceptAnswer(Answer answer, string userId)
    {
        EnsureAuthor(userId);
        EnsureOwns(answer);
        
        AcceptedAnswerId = answer.Id;
    }

    public void ClearAcceptedAnswer(string userId)
    {
        EnsureAuthor(userId);
        
        AcceptedAnswerId = null;
    }

    private void SetAuthor(string authorId, string authorDisplayName)
    {
        DomainGuard.AgainstNullOrWhiteSpace(authorId, Errors.EmptyAuthorId);
        DomainGuard.AgainstNullOrWhiteSpace(authorDisplayName, Errors.EmptyAuthorDisplayName);

        if (authorId.Length > UserIdRules.MaxLength)
            throw new DomainException(Errors.IdTooLong);

        if (authorDisplayName.Length > MaxDisplayNameLength)
            throw new DomainException(Errors.DisplayNameTooLong);

        AuthorId = authorId;
        AuthorDisplayName =  authorDisplayName;
    }
    
    private void SetContent(string title, string body)
    {
        DomainGuard.AgainstNullOrWhiteSpace(title, Errors.EmptyTitle);
        DomainGuard.AgainstNullOrWhiteSpace(body, Errors.EmptyBody);

        // Trim before measuring, so surrounding whitespace never counts against the limit.
        title = title.Trim();

        if (title.Length > MaxTitleLength)
            throw new DomainException(Errors.TitleTooLong);

        if (body.Length > MaxBodyLength)
            throw new DomainException(Errors.BodyTooLong);

        Title = title;
        Body = body;
    }

    private void SetTags(IEnumerable<Tag> tags)
    {
        var distinct = tags.DistinctBy(t => t.Id).ToList();
        
        if (distinct.Count is 0 or > MaxTags)
            throw new DomainException(Errors.TagsOutOfRange);

        _tags.Clear();
        _tags.AddRange(distinct);
    }

    private void EnsureAuthor(string userId)
    {
        DomainGuard.AgainstNullOrWhiteSpace(userId, Errors.EmptyUserId);
        
        if (userId != AuthorId)
            throw new DomainException(Errors.InvalidUser);
    }

    private void EnsureOwns(Answer answer)
    {
        DomainGuard.AgainstNull(answer, Errors.NullAnswer);
        
        if (answer.QuestionId != Id) 
            throw new DomainException(Errors.InvalidAnswer);
    }

    private static class Errors
    {
        public static readonly DomainError NullAnswer =
            new("question.null_answer", "The answer must not be null.");
            
        public static readonly DomainError EmptyAuthorId =
            new("question.empty_author_id", "The question author id must not be null or empty.");
            
        public static readonly DomainError EmptyAuthorDisplayName =
            new("question.empty_author_display_name", "The question author display must not be null or empty.");
            
        public static readonly DomainError EmptyBody =
            new("question.empty_body", "The question body must not be null or empty.");
        
        public static readonly DomainError EmptyTitle =
            new("question.empty_title", "The question title must not be null or empty.");

        public static readonly DomainError EmptyUserId =
            new("question.empty_user_id", "The user id must not be null or empty.");

        public static readonly DomainError BodyTooLong =
            new("question.body_too_long", $"The question body exceeds {MaxBodyLength} characters.");

        public static readonly DomainError TitleTooLong =
            new("question.title_too_long", $"The question title exceeds {MaxTitleLength} characters.");

        public static readonly DomainError BookIdTooLong =
            new("question.book_id_too_long", $"The book id exceeds {MaxBookIdLength} characters.");

        public static readonly DomainError IdTooLong =
            new("question.author_id_too_long", $"The author id exceeds {UserIdRules.MaxLength} characters.");

        public static readonly DomainError DisplayNameTooLong =
            new("question.author_display_name_too_long", $"The author display name exceeds {MaxDisplayNameLength} characters.");
        
        public static readonly DomainError InvalidAnswer =
            new("question.invalid_answer", "The answer doesn't belong to this question.");
        
        public static readonly DomainError InvalidUser =
            new("question.invalid_user", "Only the author is allowed to do this operation.");
        
        public static readonly DomainError TagsOutOfRange =
            new("question.tags_out_of_range", $"A question needs between 1 and {MaxTags} tags.");
    }
}