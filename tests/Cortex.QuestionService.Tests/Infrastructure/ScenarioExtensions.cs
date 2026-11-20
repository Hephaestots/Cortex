namespace Cortex.QuestionService.Tests.Infrastructure;

public static class ScenarioExtensions
{
    /// <summary>Sends the request as <paramref name="user"/> (see <see cref="TestAuthHandler"/>).</summary>
    public static Scenario As(this Scenario scenario, TestUser user)
    {
        scenario.WithRequestHeader(TestAuthHandler.HeaderName, user.UserName);
        return scenario;
    }
}
