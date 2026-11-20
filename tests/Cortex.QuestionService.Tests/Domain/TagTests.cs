using Cortex.QuestionService.Domain;
using static Cortex.QuestionService.Tests.Domain.DomainBuilders;

namespace Cortex.QuestionService.Tests.Domain;

public sealed class TagTests
{
    [Theory]
    [InlineData("fantasy", "fantasy")]
    [InlineData("  Science Fiction ", "science-fiction")]
    [InlineData("YOUNG ADULT", "young-adult")]
    // Only trimming, lowercasing and spaces: other punctuation is kept as written.
    [InlineData("a,b", "a,b")]
    public void Normalize_trims_lowercases_and_hyphenates_spaces(string input, string expected) =>
        Tag.Normalize(input).ShouldBe(expected);

    [Fact]
    public void A_new_tag_stores_the_normalized_slug()
    {
        var tag = new Tag("Science Fiction", " Science Fiction ", "Speculative fiction.");

        tag.Slug.ShouldBe("science-fiction");
        tag.Name.ShouldBe("Science Fiction");
    }

    [Fact]
    public void The_slug_limit_applies_to_the_normalized_value()
    {
        var exactlyMax = new string('s', Tag.MaxSlugLength);

        new Tag("Name", $"  {exactlyMax}  ", "Description.").Slug.ShouldBe(exactlyMax);
        ErrorCodeOf(() => new Tag("Name", exactlyMax + "s", "Description.")).ShouldBe("tag.slug_too_long");
    }

    [Fact]
    public void Name_slug_and_description_are_required_and_limited()
    {
        ErrorCodeOf(() => new Tag("", "slug", "Description.")).ShouldBe("tag.empty_name");
        ErrorCodeOf(() => new Tag("Name", " ", "Description.")).ShouldBe("tag.empty_slug");
        ErrorCodeOf(() => new Tag("Name", "slug", "")).ShouldBe("tag.empty_description");
        ErrorCodeOf(() => new Tag(new string('n', Tag.MaxNameLength + 1), "slug", "Description.")).ShouldBe("tag.name_too_long");
        ErrorCodeOf(() => new Tag("Name", "slug", new string('d', Tag.MaxDescriptionLength + 1))).ShouldBe("tag.description_too_long");
    }
}
