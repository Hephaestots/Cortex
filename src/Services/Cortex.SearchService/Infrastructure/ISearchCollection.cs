using Typesense;

namespace Cortex.SearchService.Infrastructure;

/// <summary>
/// One Typesense collection: its name and its schema. Each collection implements this next to
/// its document model (under <c>Features/</c>), and <see cref="SearchCollectionsInitializer"/>
/// creates whatever is registered, so adding a collection never touches the initializer.
/// </summary>
public interface ISearchCollection
{
    string Name { get; }

    /// <summary>
    /// Takes the name rather than using <see cref="Name"/>, so the same definition can later
    /// build a versioned collection (<c>questions_v2</c>) behind an alias for a rebuild.
    /// </summary>
    Schema BuildSchema(string collectionName);
}
