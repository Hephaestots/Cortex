namespace Cortex.QuestionService.Domain;

/// <summary>
/// Rules for the identity provider's user id (the token's <c>sub</c> claim), stored as a
/// string on authors and voters.
/// </summary>
public static class UserIdRules
{
    /// <summary>
    /// OpenID Connect caps <c>sub</c> at 255 ASCII characters. Keycloak issues 36-character
    /// GUIDs today, but nothing here should depend on that.
    /// </summary>
    public const int MaxLength = 255;
}
