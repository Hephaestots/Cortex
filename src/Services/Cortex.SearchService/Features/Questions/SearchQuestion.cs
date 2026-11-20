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

    /// <summary>Searched, lowest weight. Plain text: strip any markup before indexing.</summary>
    [JsonPropertyName(Fields.Body)]
    public required string Body { get; init; }

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
    /// The question's version from the event that produced this document. Stored but not indexed:
    /// a handler compares it with the incoming event's version and skips the older one, since
    /// RabbitMQ can deliver events late or more than once.
    /// </summary>
    [JsonPropertyName(Fields.Version)]
    public long Version { get; init; }
}