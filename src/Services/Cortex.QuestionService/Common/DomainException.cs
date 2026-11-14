namespace Cortex.QuestionService.Common;

public enum DomainErrorKind
{
    /// <summary> The input breaks a rule: empty, too long, out of range, etc. </summary>
    Validation,
    
    /// <summary> The caller is not allowed to do this to this entity. </summary>
    Forbidden,
    
    /// <summary> This input is fine, but the entity's current state rules it out. </summary>
    Conflict
}

public sealed record DomainError(string Code, string Message, DomainErrorKind Kind = DomainErrorKind.Validation);

public class DomainException : Exception
{
    public DomainError Error { get; }

    public DomainException(DomainError error)
        : base(error.Message)
    {
        Error = error;
    }

    public DomainException(DomainError error, Exception innerException)
        : base(error.Message, innerException)
    {
        Error = error;
    }
}