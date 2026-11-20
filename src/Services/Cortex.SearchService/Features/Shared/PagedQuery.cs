using FluentValidation;

namespace Cortex.SearchService.Features.Shared;

/// <summary>
/// Base for list queries bound with <c>[FromQuery]</c>: <c>?page=&amp;pageSize=</c>.
/// Settable properties with defaults, so both parameters are optional.
/// </summary>
public abstract class PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    // Keeps (Page - 1) * PageSize inside an int; past it Skip would overflow and throw (500).
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Rows to skip for this page. Only valid after validation.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// The paging rules. A query's validator derives from this instead of
/// <c>AbstractValidator&lt;T&gt;</c> and adds its own rules in its constructor.
/// Abstract, so Wolverine's validator discovery never registers it on its own.
/// </summary>
public abstract class PagedQueryValidator<T> : AbstractValidator<T> where T : PagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(x => x.Page)
            .InclusiveBetween(1, PagedQuery.MaxPage);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PagedQuery.MaxPageSize);
    }
}
