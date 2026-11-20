using Cortex.SearchService.Features.Shared;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Typesense;
using Wolverine.Http;

namespace Cortex.SearchService.Features.Questions;

public static class SearchQuestionsEndpoint
{
    /// <summary>
    /// Full-text search over questions. Public, no token needed. Under <c>/api/search/</c>, not
    /// <c>/api/questions/</c>: the gateway will route by path prefix, and <c>/api/questions/*</c>
    /// belongs to the Question service.
    /// </summary>
    [WolverineGet("/api/search/questions")]
    public static async Task<Ok<SearchQuestionsResponse>> Get(
        [FromQuery] SearchQuestionsQuery query,
        ITypesenseClient client,
        CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(query.Q) ? "*" : query.Q.Trim();

        var normalizedTags = query.Tags
            .Select(tag => tag.Trim().ToLowerInvariant().Replace(' ', '-'))
            .Where(tag => tag.Length > 0)
            .Distinct();
        
        var filters = normalizedTags
            .Select(tag => $"{SearchQuestion.Fields.Tags}:=`{tag}`")
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.BookId))
            filters.Add($"{SearchQuestion.Fields.BookId}:=`{query.BookId}`");
        
        var searchParams = new SearchParameters(
            text,
            queryBy: string.Join(",", 
                SearchQuestion.Fields.Title,
                SearchQuestion.Fields.Tags,
                SearchQuestion.Fields.Body,
                SearchQuestion.Fields.AuthorDisplayName))
        {
            QueryByWeights = "4,3,1,1",
            FacetBy = query.Facets
                ? string.Join(",", 
                    SearchQuestion.Fields.Tags, 
                    SearchQuestion.Fields.HasAcceptedAnswer)
                : null,
            MaxFacetValues = 10,
            FilterBy = filters.Count > 0 ? string.Join(" && ", filters) : null,
            Offset = query.Skip,
            Limit = query.PageSize,
            ExcludeFields = string.Join(",",
                SearchQuestion.Fields.Body,
                SearchQuestion.Fields.Version),
            HighlightFullFields = SearchQuestion.Fields.Title,
            HighlightFields = string.Join(",",
                SearchQuestion.Fields.Title,
                SearchQuestion.Fields.Body),
            SortBy = string.Equals(text, "*") 
                ? $"{SearchQuestion.Fields.CreatedAt}:desc" 
                : $"_text_match:desc,{SearchQuestion.Fields.CreatedAt}:desc",
            UseCache = true,
            CacheTtl = 60,
            LimitHits = SearchQuestionsQuery.MaxResults,
            HighlightStartTag = "\u0001",
            HighlightEndTag = "\u0002"
        };
        
        var result = await client.Search<SearchQuestion>(
            SearchQuestion.CollectionName, 
            searchParams, 
            ct);

        var facets = query.Facets
            ? result.FacetCounts
                .Select(facet => new SearchFacet(
                    facet.FieldName,
                    [.. facet.Counts.Select(count => new SearchFacetValue(count.Value, count.Count))]))
                .ToList()
            : null;

        return TypedResults.Ok(new SearchQuestionsResponse(
            [.. result.Hits.Select(QuestionSearchResult.From)],
            query.Page,
            query.PageSize,
            TotalCount: Math.Min(result.Found, SearchQuestionsQuery.MaxResults),
            Found: result.Found,
            facets));
    }
}

public sealed class SearchQuestionsQuery : PagedQuery
{
    public const int MaxResults = 1000;
    
    public string Q { get; set; } = "*";

    public string[] Tags { get; set; } = [];

    public string? BookId { get; set; }
    
    public bool Facets { get; set; }

    public sealed class SearchQuestionsQueryValidator : PagedQueryValidator<SearchQuestionsQuery>
    {
        public SearchQuestionsQueryValidator()
        {
            RuleFor(query => query)
                .Must(query => (long)query.Page * query.PageSize <= MaxResults)
                .WithName(nameof(Page))
                .WithMessage($"Search results go no deeper than {MaxResults}. Refine the search instead.");

            RuleForEach(query => query.Tags)
                .MaximumLength(100)
                .Must(tag => !tag.Contains('`'))
                .WithMessage("A tag must not contain a backtick.");

            RuleFor(query => query.BookId)
                .MaximumLength(100)
                .Must(bookId => bookId is null || !bookId.Contains('`'))
                .WithMessage("A book id must not contain a backtick.");

            RuleFor(query => query.Tags)
                .Must(s => s is { Length: <= 5 })
                .WithMessage("You can filter by at most 5 tags.");

            RuleFor(query => query.Q)
                .MaximumLength(200);
        }
    }
}

/// <summary>
/// One hit, as the client sees it: not <see cref="SearchQuestion"/>, so the index can change
/// (and keep fields like <c>version</c>) without breaking clients.
/// </summary>
public sealed record QuestionSearchResult(
    Guid Id,
    string Title,
    string? TitleHighlighted,
    string? Snippet,
    IReadOnlyList<string> Tags,
    string AuthorDisplayName,
    string? BookId,
    int Score,
    int AnswerCount,
    bool HasAcceptedAnswer,
    DateTimeOffset CreatedAt)
{
    public static QuestionSearchResult From(Hit<SearchQuestion> hit)
    {
        var document = hit.Document;

        return new QuestionSearchResult(
            Guid.Parse(document.Id),
            document.Title,
            HighlightOf(SearchQuestion.Fields.Title),
            HighlightOf(SearchQuestion.Fields.Body),
            document.Tags,
            document.AuthorDisplayName,
            document.BookId,
            document.Score,
            document.AnswerCount,
            document.HasAcceptedAnswer,
            DateTimeOffset.FromUnixTimeSeconds(document.CreatedAt));

        string? HighlightOf(string field)
        {
            var highlight = hit.Highlights?.FirstOrDefault(h => h.Field == field);

            // The title is listed in HighlightFullFields.
            return field == SearchQuestion.Fields.Title 
                ? highlight?.Value 
                : highlight?.Snippet;
        }
    }
}

public sealed record SearchQuestionsResponse(
    IReadOnlyList<QuestionSearchResult> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int Found,
    IReadOnlyList<SearchFacet>? Facets)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

public sealed record SearchFacet(string Field, IReadOnlyList<SearchFacetValue> Values);

public sealed record SearchFacetValue(string Value, int Count);
