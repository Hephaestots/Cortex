namespace Cortex.QuestionService.Data;

/// <summary>
/// The starting set of tags. <see cref="TagSeeder"/> inserts the ones that are missing.
/// </summary>
/// <remarks>
/// Write slugs lowercase and hyphenated, with no punctuation. <c>Tag</c> only trims, lowercases
/// and replaces spaces, so anything else in a slug would end up in URLs as written.
/// </remarks>
public static class TagSeed
{
    public sealed record Entry(string Name, string Slug, string Description);

    public static readonly IReadOnlyList<Entry> All =
    [
        // Genres and forms
        new("Fantasy", "fantasy", "Books set in invented worlds or involving magic, from epic and high fantasy to urban fantasy."),
        new("Science Fiction", "science-fiction", "Speculative fiction built on science and technology: space opera, dystopias, cyberpunk, hard SF."),
        new("Mystery", "mystery", "Detective stories, whodunits and crime fiction where solving a puzzle drives the plot."),
        new("Thriller", "thriller", "Suspense-driven fiction: psychological, legal, spy and action thrillers."),
        new("Horror", "horror", "Fiction meant to frighten or unsettle, including gothic and supernatural horror."),
        new("Romance", "romance", "Stories centred on a romantic relationship, in any setting or subgenre."),
        new("Historical Fiction", "historical-fiction", "Fiction set in a real past period. Includes questions about historical accuracy."),
        new("Literary Fiction", "literary-fiction", "Character- and style-driven fiction, contemporary or modern."),
        new("Classics", "classics", "Works with lasting literary standing, from antiquity to the twentieth century."),
        new("Young Adult", "young-adult", "Books written for teenage readers, in any genre."),
        new("Children's Books", "childrens-books", "Picture books, chapter books and middle-grade fiction."),
        new("Non-Fiction", "non-fiction", "Factual books: history, science, essays, self-help and more."),
        new("Biography & Memoir", "biography-memoir", "Accounts of real lives, written by the subject or by someone else."),
        new("Poetry", "poetry", "Poems, collections and verse novels, including questions about reading and interpreting them."),
        new("Graphic Novels", "graphic-novels", "Comics, manga and graphic novels."),
        new("Short Stories", "short-stories", "Short fiction, novellas, collections and anthologies."),

        // Kinds of question
        new("Recommendations", "recommendations", "Requests for what to read next. Say what you liked and what you are looking for."),
        new("Book Identification", "book-identification", "Help finding a book you only partly remember. Include every detail you can: plot, cover, when you read it."),
        new("Plot Explanation", "plot-explanation", "Questions about what happened in a book and why. Mark spoilers."),
        new("Ending Explained", "ending-explained", "Questions about how a book ends and what the ending means. Expect spoilers."),
        new("Character Analysis", "character-analysis", "Questions about a character's motives, development or role in the story."),
        new("Themes & Symbolism", "themes-symbolism", "Questions about the ideas, motifs and symbols in a work and how to interpret them."),
        new("Reading Order", "reading-order", "Which order to read a series or an author's works in: publication, chronological or other."),
        new("Series", "series", "Questions that span several books in a series, such as continuity or whether a series is finished."),
        new("Authors", "authors", "Questions about a writer's life, influences, intentions or body of work."),
        new("Adaptations", "adaptations", "Film, television, stage and game adaptations, and how they differ from the book."),
        new("Translations", "translations", "Choosing between translations, and questions about how a passage was translated."),
        new("Editions", "editions", "Differences between editions, printings, abridgements and revised texts."),
        new("Quotes", "quotes", "Finding the source of a quotation or understanding what a passage means."),

        // Reading life
        new("Audiobooks", "audiobooks", "Narrators, productions and listening to books."),
        new("E-books", "ebooks", "E-readers, formats and reading digitally."),
        new("Book Clubs", "book-clubs", "Running a reading group, choosing books for one and discussion prompts."),
    ];
}
