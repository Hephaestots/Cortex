namespace Cortex.QuestionService.Common;

/// <summary>
/// An entity a client can change concurrently with someone else, guarded by optimistic
/// concurrency: reads return its <see cref="Version"/>, writes send back the one they read,
/// and a write based on an old version is rejected with 409 instead of overwriting.
/// </summary>
/// <remarks>
/// The database sets the version, never the domain: it is Postgres's <c>xmin</c> system
/// column, which changes on every update of the row (<c>IsRowVersion()</c> in the entity's
/// configuration). See "Concurrency" in docs/status.md.
/// </remarks>
public interface IVersioned
{
    uint Version { get; }
}
