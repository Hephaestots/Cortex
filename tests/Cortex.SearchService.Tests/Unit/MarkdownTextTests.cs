using Cortex.SearchService.Infrastructure;

namespace Cortex.SearchService.Tests.Unit;

/// <summary>
/// <see cref="MarkdownText.ToPlainText"/>: the index must hold the words a reader sees on the page,
/// which renders Markdown with raw HTML disabled. No host or container involved.
/// </summary>
public sealed class MarkdownTextTests
{
    [Theory]
    [InlineData("# A heading", "A heading")]
    [InlineData("Some **bold**, *italic* and `code`.", "Some bold, italic and code.")]
    [InlineData(@"\*not italic\*", "*not italic*")]
    // A hash inside a sentence is text, not a heading: a regex that strips "#" would get this wrong.
    [InlineData("Is C# a good first language?", "Is C# a good first language?")]
    public void Formatting_is_removed_and_the_words_are_kept(string markdown, string expected) =>
        MarkdownText.ToPlainText(markdown).ShouldBe(expected);

    [Theory]
    // The link text is what a reader sees; the target is not, so it isn't searchable either.
    [InlineData("Read [the review](https://example.com/review) first.", "Read the review first.")]
    [InlineData("![The cover](https://example.com/cover.png)", "The cover")]
    [InlineData("See <https://example.com>.", "See https://example.com.")]
    public void Links_and_images_keep_only_what_a_reader_sees(string markdown, string expected) =>
        MarkdownText.ToPlainText(markdown).ShouldBe(expected);

    [Fact]
    public void A_code_block_keeps_its_code_without_the_fences()
    {
        var markdown = "Before\n\n```csharp\nvar answer = 42;\n```\n\nAfter";

        MarkdownText.ToPlainText(markdown).ShouldBe("Before var answer = 42; After");
    }

    [Fact]
    public void Paragraphs_and_list_items_are_joined_with_single_spaces()
    {
        var markdown = "  First paragraph.\n\n\n- one\n- two\n\n1. three\n\nLast   one.  ";

        MarkdownText.ToPlainText(markdown).ShouldBe("First paragraph. one two three Last one.");
    }

    [Theory]
    // Angle brackets in ordinary text survive; a regex removing <...> would eat "< 5 and 7 >".
    [InlineData("3 < 5 and 7 > 4", "3 < 5 and 7 > 4")]
    // Raw HTML is shown as typed (DisableHtml), so it is indexed as typed: tags included. Without
    // DisableHtml inline tags vanish ("Some raw HTML"); an HTML block like the script comes out the
    // same either way, so only the first case guards the setting.
    [InlineData("Some <b>raw</b> HTML", "Some <b>raw</b> HTML")]
    [InlineData("<script>alert(1)</script>", "<script>alert(1)</script>")]
    // An entity is shown as its character, so it is indexed as that character.
    [InlineData("Fish &amp; chips", "Fish & chips")]
    public void Html_is_indexed_as_the_literal_text_a_reader_sees(string markdown, string expected) =>
        MarkdownText.ToPlainText(markdown).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void An_empty_body_gives_empty_text(string markdown) =>
        MarkdownText.ToPlainText(markdown).ShouldBe("");
}
