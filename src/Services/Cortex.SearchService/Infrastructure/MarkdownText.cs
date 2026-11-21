using System.Text.RegularExpressions;
using Markdig;

namespace Cortex.SearchService.Infrastructure;

/// <summary>
/// Turns a Markdown body (questions and answers are Markdown, see CLAUDE.md) into the plain text a
/// reader sees, for indexing: no <c>#</c>, <c>**</c>, link targets or code fences in the index or in
/// highlighted snippets.
/// </summary>
/// <remarks>
/// <para>
/// A Markdown parser, not a regex: a regex that removes <c>&lt;...&gt;</c> also eats ordinary text
/// such as "3 &lt; 5 and 7 &gt; 4", and can't tell a heading from a hash in a sentence.
/// </para>
/// <para>
/// <c>DisableHtml</c> mirrors how the frontend renders bodies (Markdown with raw HTML turned off):
/// raw HTML a user typed is shown as literal text, so it is indexed as literal text too. The index
/// then matches what readers see, and snippets never carry live markup (the frontend escapes them).
/// </para>
/// </remarks>
public static partial class MarkdownText
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();

    public static string ToPlainText(string markdown) =>
        // Paragraphs and list items come out on separate lines; one space reads better in snippets.
        Whitespace().Replace(Markdown.ToPlainText(markdown, Pipeline), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
