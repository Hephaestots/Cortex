namespace Cortex.QuestionService.Common;

internal static class DomainGuard
{
    public static T AgainstNull<T>(T? value, DomainError error) where T : class
        => value ?? throw new DomainException(error);

    public static string AgainstNullOrWhiteSpace(string? value, DomainError error)
        => string.IsNullOrWhiteSpace(value) ? throw new DomainException(error) : value;

    public static Guid AgainstEmpty(Guid value, DomainError error)
        => value == Guid.Empty ? throw new DomainException(error) : value;
}