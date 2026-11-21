using System.Text.Json.Serialization;

namespace Cortex.SearchService.Features.Questions;

public sealed class SearchQuestion
{
    public const string CollectionName = "questions";

    public static class Fields
    {
        public const string Id = "id";
        public const string Title = "title";
        public const string Body = "body";
        public const string Tags = "tags";
        public const string AuthorId = "author_id";
        public const string AuthorDisplayName = "author_display_name";
        public const string BookId = "book_id";
        public const string Score = "score";
        public const string AnswerCount = "answer_count";
        public const string HasAcceptedAnswer = "has_accepted_answer";
        public const string CreatedAt = "created_at";
        public const string LastActivityAt = "last_activity_at";
        public const string Version = "version";
    }
    
    /// <summary>The question's id as a string: Typesense document ids are strings.</summary>
    [JsonPropertyName(Fields.Id)]
    public required string Id { get; init; }

    /// <summary>Searched, with the highest weight.</summary>
    [JsonPropertyName(Fields.Title)]
    public required string Title { get; init; }

    /// <summary>Searched, lowest weight. The Markdown body as plain text (<c>MarkdownText.ToPlainText</c>).</summary>
    /// <remarks>
    /// Not <c>required</c>, unlike the other text fields: searches exclude the body from their hits
    /// (<c>exclude_fields</c>), and a required property would make every hit fail to deserialize.
    /// </remarks>
    [JsonPropertyName(Fields.Body)]
    public string Body { get; init; } = "";

    /// <summary>Slugs. Searched and faceted ("questions tagged x"); see token_separators for hyphens.</summary>
    [JsonPropertyName(Fields.Tags)]
    public List<string> Tags { get; init; } = [];

    /// <summary>Faceted: "questions by this user", and lets results show Edit for the author.</summary>
    [JsonPropertyName(Fields.AuthorId)]
    public required string AuthorId { get; init; }

    /// <summary>Shown in results and searchable at a low weight.</summary>
    [JsonPropertyName(Fields.AuthorDisplayName)]
    public required string AuthorDisplayName { get; init; }

    /// <summary>Open Library work key. Faceted ("questions about this book"); optional in the schema.</summary>
    [JsonPropertyName(Fields.BookId)]
    public string? BookId { get; init; }

    /// <summary>Sum of the votes. Sortable. Stays 0 until vote events reach the Search service.</summary>
    [JsonPropertyName(Fields.Score)]
    public int Score { get; init; }

    /// <summary>Sortable. Needs answer events (posted, deleted) to stay current.</summary>
    [JsonPropertyName(Fields.AnswerCount)]
    public int AnswerCount { get; init; }

    /// <summary>Faceted ("unanswered"). Needs answer events (accepted, cleared, deleted) to stay current.</summary>
    [JsonPropertyName(Fields.HasAcceptedAnswer)]
    public bool HasAcceptedAnswer { get; init; }

    /// <summary>Unix seconds. The default sorting field (newest first).</summary>
    [JsonPropertyName(Fields.CreatedAt)]
    public long CreatedAt { get; init; }

    /// <summary>Unix seconds of the last edit, answer or vote. Sortable ("recently active").</summary>
    [JsonPropertyName(Fields.LastActivityAt)]
    public long LastActivityAt { get; init; }

    /// <summary>
    /// The question's version from the last create or edit applied here. Stored but not indexed: the
    /// edit handler skips an event that is not newer, since RabbitMQ can deliver events late or more
    /// than once. Only <c>QuestionCreated</c> and <c>QuestionUpdated</c> set it: an answer or vote
    /// event that did would make a delayed edit look old and lose it.
    /// </summary>
    [JsonPropertyName(Fields.Version)]
    public long Version { get; init; }
}

/// <summary>
/// The fields a <c>QuestionUpdated</c> owns, sent as a partial update. Not <see cref="SearchQuestion"/>:
/// its defaults (a score of 0, say) would be sent too and overwrite the indexed values.
/// </summary>
public sealed class SearchQuestionEdit
{
    [JsonPropertyName(SearchQuestion.Fields.Title)]
    public required string Title { get; init; }

    [JsonPropertyName(SearchQuestion.Fields.Body)]
    public required string Body { get; init; }

    [JsonPropertyName(SearchQuestion.Fields.Tags)]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName(SearchQuestion.Fields.LastActivityAt)]
    public long LastActivityAt { get; init; }

    [JsonPropertyName(SearchQuestion.Fields.Version)]
    public long Version { get; init; }
}

/// <summary>
/// Only the indexed version, read by the edit handler before it applies an edit. 0 for a document
/// indexed before versions were stored, so any edit wins.
/// </summary>
public sealed class SearchQuestionVersion
{
    [JsonPropertyName(SearchQuestion.Fields.Version)]
    public long Version { get; init; }
}