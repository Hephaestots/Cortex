namespace Cortex.QuestionService.Domain;

/// <summary>
/// Limits for the user data this service copies from the token onto authors and voters:
/// the identity provider's user id (<c>sub</c>) and the display name.
/// </summary>
public static class UserRules
{
    /// <summary>
    /// OpenID Connect caps <c>sub</c> at 255 ASCII characters. Keycloak issues 36-character
    /// GUIDs today, but nothing here should depend on that.
    /// </summary>
    public const int MaxIdLength = 255;

    /// <summary>
    /// The column width for the denormalized author name (<c>preferred_username</c>). It is our
    /// own cap, not a Keycloak limit: Keycloak allows longer usernames, and a longer one is
    /// rejected by the domain rather than truncated.
    /// </summary>
    public const int MaxDisplayNameLength = 100;
}
