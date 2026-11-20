using Typesense;

namespace Cortex.SearchService.Infrastructure;

/// <summary>
/// Creates every registered <see cref="ISearchCollection"/> that Typesense does not have yet.
/// Called once at startup, before the service handles any message.
/// </summary>
/// <remarks>
/// Create-if-missing only: an existing collection is left as it is, so a schema change in code
/// does not reach Typesense. Changing a schema means a new versioned collection, rebuilt and
/// then swapped in behind an alias (see "Next up" in docs/status.md).
/// </remarks>
public sealed class SearchCollectionsInitializer(
    ITypesenseClient client,
    IEnumerable<ISearchCollection> collections,
    ILogger<SearchCollectionsInitializer> logger)
{
    public async Task EnsureCollectionsExistAsync(CancellationToken ct)
    {
        foreach (var collection in collections)
        {
            if (await ExistsAsync(collection.Name, ct))
            {
                logger.LogInformation("Search collection {Collection} already exists", collection.Name);
                continue;
            }

            try
            {
                await client.CreateCollection(collection.BuildSchema(collection.Name), ct);
                logger.LogInformation("Created search collection {Collection}", collection.Name);
            }
            catch (TypesenseApiConflictException)
            {
                // Another instance created it between our check and our create: same end state.
                logger.LogInformation("Search collection {Collection} was created concurrently", collection.Name);
            }
        }
    }

    /// <summary>Typesense has no "exists" call, only "retrieve, or 404".</summary>
    private async Task<bool> ExistsAsync(string name, CancellationToken ct)
    {
        try
        {
            await client.RetrieveCollection(name, ct);
            return true;
        }
        catch (TypesenseApiNotFoundException)
        {
            return false;
        }
    }
}
