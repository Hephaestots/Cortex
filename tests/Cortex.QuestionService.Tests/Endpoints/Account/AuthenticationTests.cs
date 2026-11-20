using Cortex.QuestionService.Features.Account;
using Cortex.QuestionService.Tests.Infrastructure;

namespace Cortex.QuestionService.Tests.Endpoints.Account;

/// <summary>
/// Checks the test authentication itself, so later endpoint tests can trust 401 / 403 / 200:
/// anonymous requests stay anonymous, a test user reaches <c>CurrentUser</c> with the right
/// claims, and roles drive the <c>AdminOnly</c> policy.
/// </summary>
public sealed class AuthenticationTests(AppFixture fixture)
{
    [Fact]
    public async Task Me_without_a_user_is_401()
    {
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/account/me");
            s.StatusCodeShouldBe(401);
        });
    }

    [Fact]
    public async Task Me_returns_the_callers_claims_through_CurrentUser()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Admin);
            s.Get.Url("/api/account/me");
            s.StatusCodeShouldBeOk();
        });

        var me = await result.ReadAsJsonAsync<MeResponse>();

        me.ShouldNotBeNull();
        me.UserId.ShouldBe(TestUsers.Admin.Id);
        me.UserName.ShouldBe(TestUsers.Admin.UserName);
        me.Roles.ShouldBe(TestUsers.Admin.Roles, ignoreOrder: true);
    }

    [Fact]
    public async Task Admin_ping_is_403_for_a_user_without_the_admin_role()
    {
        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Reader);
            s.Get.Url("/api/admin/ping");
            s.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task Admin_ping_is_200_for_an_admin()
    {
        await fixture.Host.Scenario(s =>
        {
            s.As(TestUsers.Admin);
            s.Get.Url("/api/admin/ping");
            s.StatusCodeShouldBeOk();
        });
    }
}
