using Cortex.QuestionService.Common;

namespace Cortex.QuestionService.Domain;

public sealed class Tag
{
    public const int MaxNameLength = 50;
    public const int MaxSlugLength = 100;
    public const int MaxDescriptionLength = 1000;

    private readonly List<Question> _questions = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    public IReadOnlyCollection<Question> Questions => _questions;

    private Tag() { }

    public Tag(string name, string slug, string description)
    {
        Id = Guid.CreateVersion7();

        DomainGuard.AgainstNullOrWhiteSpace(name, Errors.EmptyName);
        DomainGuard.AgainstNullOrWhiteSpace(slug, Errors.EmptySlug);
        DomainGuard.AgainstNullOrWhiteSpace(description, Errors.EmptyDescription);

        if (name.Length > MaxNameLength)
            throw new DomainException(Errors.NameTooLong);

        // Checked after normalising, so the limit applies to the value that is stored.
        var normalizedSlug = Normalize(slug);
        if (normalizedSlug.Length > MaxSlugLength)
            throw new DomainException(Errors.SlugTooLong);

        if (description.Length > MaxDescriptionLength)
            throw new DomainException(Errors.DescriptionTooLong);

        Name = name;
        Slug = normalizedSlug;
        Description = description;
    }

    private static string Normalize(string slug) =>
        slug.Trim().ToLowerInvariant().Replace(' ', '-');

    private static class Errors
    {
        public static readonly DomainError NameTooLong =
            new("tag.name_too_long", $"The tag name exceeds {MaxNameLength} characters.");

        public static readonly DomainError SlugTooLong =
            new("tag.slug_too_long", $"The tag slug exceeds {MaxSlugLength} characters.");

        public static readonly DomainError DescriptionTooLong =
            new("tag.description_too_long", $"The tag description exceeds {MaxDescriptionLength} characters.");

        public static readonly DomainError EmptyName =
            new("tag.empty_name", "The tag name must not be null or empty.");

        public static readonly DomainError EmptySlug =
            new("tag.empty_slug", "The tag slug must not be null or empty.");

        public static readonly DomainError EmptyDescription =
            new("tag.empty_description", "The tag description must not be null or empty.");
    }
}