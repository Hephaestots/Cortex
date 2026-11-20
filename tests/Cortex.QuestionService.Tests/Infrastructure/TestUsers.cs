using Cortex.ServiceDefaults;

namespace Cortex.QuestionService.Tests.Infrastructure;

/// <summary>
/// A caller as the service sees it after JWT validation: the claims <c>CurrentUser</c> reads.
/// </summary>
public sealed record TestUser(string UserName, string Id, IReadOnlyList<string> Roles);

/// <summary>
/// Stand-ins for the Keycloak test users in <c>cortex-realm.json</c>. The ids are fixed so a
/// test can compare <c>authorId</c> in a response; they don't need to match Keycloak's.
/// </summary>
public static class TestUsers
{
    public static readonly TestUser Reader =
        new("reader", "00000000-0000-0000-0000-00000000000a", [Roles.User]);

    public static readonly TestUser Author =
        new("hephaestots", "00000000-0000-0000-0000-00000000000b", [Roles.User]);

    public static readonly TestUser Admin =
        new("admin-user", "00000000-0000-0000-0000-00000000000c", [Roles.User, Roles.Admin]);

    public static IReadOnlyList<TestUser> All { get; } = [Reader, Author, Admin];
}
