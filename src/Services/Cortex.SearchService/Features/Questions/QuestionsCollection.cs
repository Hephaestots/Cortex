using Cortex.SearchService.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Features.Questions;

/// <summary>
/// The schema of the "questions" collection. It decides what is searched, faceted and stored;
/// <see cref="SearchQuestion"/> only shapes the JSON, with the same field names.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Searched (listed in <c>query_by</c> at query time): title, tags, body, author_display_name.</item>
/// <item>Faceted, for counts like "fantasy (12)": tags, author_id, book_id, has_accepted_answer.</item>
/// <item>Numbers are sortable by default in Typesense, so they need no <c>sort</c> flag.</item>
/// <item><c>book_id</c> is optional: a question without a book has none, and Typesense rejects a
/// document that lacks a non-optional field.</item>
/// <item><c>version</c> is stored for the handlers' out-of-order check, not indexed (Typesense
/// requires a field with <c>index: false</c> to be optional).</item>
/// </list>
/// </remarks>
public sealed class QuestionsCollection : ISearchCollection
{
    public string Name => SearchQuestion.CollectionName;

    public Schema BuildSchema(string collectionName) =>
        new(collectionName,
            fields:
            [
                new Field(SearchQuestion.Fields.Title, FieldType.String),
                new Field(SearchQuestion.Fields.Body, FieldType.String),
                new Field(SearchQuestion.Fields.Tags, FieldType.StringArray, facet: true),
                new Field(SearchQuestion.Fields.AuthorId, FieldType.String, facet: true),
                new Field(SearchQuestion.Fields.AuthorDisplayName, FieldType.String),
                new Field(SearchQuestion.Fields.BookId, FieldType.String, facet: true, optional: true),
                new Field(SearchQuestion.Fields.Score, FieldType.Int32),
                new Field(SearchQuestion.Fields.AnswerCount, FieldType.Int32),
                new Field(SearchQuestion.Fields.HasAcceptedAnswer, FieldType.Bool, facet: true),
                new Field(SearchQuestion.Fields.CreatedAt, FieldType.Int64),
                new Field(SearchQuestion.Fields.LastActivityAt, FieldType.Int64),
                // Properties, not constructor arguments: index + optional matches two overloads.
                new Field(SearchQuestion.Fields.Version, FieldType.Int64) { Index = false, Optional = true }
            ])
        {
            DefaultSortingField = SearchQuestion.Fields.CreatedAt,
            // Split on hyphens too, so the slug "science-fiction" matches a search for "science fiction".
            TokenSeparators = ["-"]
        };
}
