using Cortex.SearchService.Features.Questions;
using Cortex.SearchService.Tests.Infrastructure;
using Typesense;

namespace Cortex.SearchService.Tests.Startup;

/// <summary>
/// The smoke test: the fixture's container and host work together, and startup did its part.
/// </summary>
public sealed class StartupTests(AppFixture fixture)
{
    [Fact]
    public async Task Startup_creates_the_questions_collection()
    {
        var client = fixture.Host.Services.GetRequiredService<ITypesenseClient>();

        var collection = await client.RetrieveCollection(
            SearchQuestion.CollectionName,
            TestContext.Current.CancellationToken);

        collection.Name.ShouldBe(SearchQuestion.CollectionName);
    }

    [Fact]
    public async Task Search_answers_anonymously()
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/search/questions");
            s.StatusCodeShouldBeOk();
        });
    }
}
