using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Tests.Endpoints.Questions;

public sealed class CreateQuestionTests(AppFixture fixture)
{
    private static CreateQuestionRequest ValidRequest(IReadOnlyList<string>? tags = null) =>
        new("Is the narrator reliable?", "I keep doubting him in the last chapters.", null, tags ?? [TestData.AnyTag]);

    [Fact]
    public async Task Without_a_user_is_401()
    {
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(ValidRequest()).ToUrl("/api/questions");
            s.StatusCodeShouldBe(401);
        });
    }

    [Fact]
    public async Task Creates_the_question_for_the_caller_and_returns_201_with_its_location()
    {
        var request = ValidRequest(tags: ["Horror", "fantasy"]) with { BookId = "OL27448W" };

        var result = await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Post.Json(request).ToUrl("/api/questions");
            s.StatusCodeShouldBe(201);
        });

        var created = await result.ReadAsJsonAsync<CreateQuestionResponse>();

        created.ShouldNotBeNull();
        result.Context.Response.Headers.Location.ToString().ShouldBe($"/api/questions/{created.Id}");
        created.Title.ShouldBe(request.Title);
        created.AuthorId.ShouldBe(TestUsers.Reader.Id);
        created.AuthorDisplayName.ShouldBe(TestUsers.Reader.UserName);
        created.BookId.ShouldBe("OL27448W");
        // Normalized like every slug: "Horror" is the seeded "horror".
        created.TagSlugs.ShouldBe(["horror", "fantasy"], ignoreOrder: true);
        created.Version.ShouldNotBe(0u);

        // The view counter row is inserted with the question, at 0.
        var counter = await fixture.WithDbAsync(db => db.QuestionViewCounts.SingleAsync(c => c.QuestionId == created.Id));
        counter.Count.ShouldBe(0);

        // What GET reads back is what was saved, with the same version.
        var stored = await fixture.GetQuestionAsync(created.Id);
        stored.Body.ShouldBe(request.Body);
        stored.Version.ShouldBe(created.Version);
    }

    [Fact]
    public async Task An_empty_title_is_400_under_Title()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Post.Json(ValidRequest() with { Title = "" }).ToUrl("/api/questions");
            s.StatusCodeShouldBe(400);
        });

        (await result.ReadProblemAsync()).ErrorsFor("Title")
            .ShouldBe(["The question title must not be null or empty."]);
    }

    [Fact]
    public async Task Six_tags_is_400_under_TagSlugs()
    {
        string[] sixTags = ["fantasy", "horror", "poetry", "mystery", "romance", "classics"];

        var result = await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Post.Json(ValidRequest(sixTags)).ToUrl("/api/questions");
            s.StatusCodeShouldBe(400);
        });

        (await result.ReadProblemAsync()).ErrorsFor("TagSlugs")
            .ShouldBe(["A question needs between 1 and 5 tags."]);
    }

    [Fact]
    public async Task Unknown_tags_are_400_naming_each_one_sorted_and_normalized()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Post.Json(ValidRequest(["ZZZ", "fantasy", "nope"])).ToUrl("/api/questions");
            s.StatusCodeShouldBe(400);
        });

        (await result.ReadProblemAsync()).ErrorsFor("TagSlugs")
            .ShouldBe(["Tag 'nope' does not exist.", "Tag 'zzz' does not exist."]);
    }
}
