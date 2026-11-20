using Cortex.QuestionService.Common;
using Cortex.QuestionService.Domain;

namespace Cortex.QuestionService.Tests.Domain;

/// <summary>
/// Valid domain objects for unit tests, built in memory: nothing here touches the database,
/// so ids come from the domain and <c>Version</c> stays 0.
/// </summary>
internal static class DomainBuilders
{
    public const string AuthorId = "author-sub";
    public const string OtherUserId = "other-sub";

    public static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    public static Tag NewTag(string slug = "fantasy") => new(slug, slug, $"About {slug}.");

    public static Question NewQuestion(
        string title = "Is the narrator reliable?",
        string body = "I keep doubting him.",
        string authorId = AuthorId,
        string authorDisplayName = "author",
        string? bookId = null,
        IEnumerable<Tag>? tags = null) =>
        new(title, body, authorId, authorDisplayName, bookId, tags ?? [NewTag()], Now);

    public static Answer NewAnswer(Question question, string authorId = OtherUserId) =>
        question.AddAnswer("Read it again.", authorId, "answerer", Now);

    /// <summary>Runs <paramref name="act"/> and returns the code of the <see cref="DomainException"/> it throws.</summary>
    public static string ErrorCodeOf(Action act) => Should.Throw<DomainException>(act).Error.Code;
}
