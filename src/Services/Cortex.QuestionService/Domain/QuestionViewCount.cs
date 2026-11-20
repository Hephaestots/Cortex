namespace Cortex.QuestionService.Domain;

/// <summary>
/// How often a question was viewed, in its own row (table <c>question_view_counts</c>) rather
/// than a column of <c>questions</c>.
/// </summary>
/// <remarks>
/// <para>
/// Why a separate row: the question's version is Postgres's <c>xmin</c>, which changes on every
/// UPDATE of the row. A counter on the question row made every view a new version, so anyone
/// editing got a 409 as soon as someone else looked at the question. Here a view updates this
/// row only, and the question's version stays the version of its editable fields.
/// </para>
/// <para>
/// Created together with its question (one row per question, primary key = foreign key, deleted
/// with it), so counting is always a plain UPDATE, never an insert-if-missing. The count only
/// ever changes in the database (<c>RecordQuestionView</c>, an atomic <c>count = count + 1</c>),
/// so there is no method for it here.
/// </para>
/// </remarks>
public sealed class QuestionViewCount
{
    public Guid QuestionId { get; private set; }
    public int Count { get; private set; }

    private QuestionViewCount() { }

    internal QuestionViewCount(Guid questionId) => QuestionId = questionId;
}
