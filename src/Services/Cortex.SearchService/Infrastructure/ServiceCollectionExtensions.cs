using Cortex.SearchService.Features.Questions;
using Typesense.Setup;

namespace Cortex.SearchService.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Typesense client from what the AppHost passes: the endpoint through service
    /// discovery (<c>services__typesense__http__0</c>) and the admin key (<c>Typesense__ApiKey</c>).
    /// Both are required, so a missing one stops the service at startup instead of failing on the
    /// first request.
    /// </summary>
    /// <remarks>
    /// Extends the builder rather than <c>IServiceCollection</c> because it reads the configuration,
    /// like <c>AddQuestionServiceData</c> in the Question service.
    /// </remarks>
    public static IHostApplicationBuilder AddTypesense(this IHostApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["services:typesense:http:0"]
            ?? throw new InvalidOperationException(
                "No Typesense endpoint (services:typesense:http:0). Is the service running under the AppHost?");

        var apiKey = builder.Configuration["Typesense:ApiKey"]
            ?? throw new InvalidOperationException(
                "No Typesense API key (Typesense:ApiKey). Is the typesense-api-key parameter set in the AppHost?");

        var uri = new Uri(endpoint);

        builder.Services.AddTypesenseClient(config =>
        {
            config.ApiKey = apiKey;
            config.Nodes = [new Node(uri.Host, uri.Port.ToString(), uri.Scheme)];
        });

        return builder;
    }

    /// <summary>The collections the service owns, and the initializer that creates them.</summary>
    public static IServiceCollection AddSearchCollections(this IServiceCollection services)
    {
        services.AddSingleton<ISearchCollection, QuestionsCollection>();
        services.AddSingleton<SearchCollectionsInitializer>();

        return services;
    }
}
