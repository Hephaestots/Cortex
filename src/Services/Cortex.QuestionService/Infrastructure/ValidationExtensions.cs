using Cortex.QuestionService.Common;
using FluentValidation;

namespace Cortex.QuestionService.Infrastructure;

internal static class ValidationExtensions
{
    /// <summary>
    /// Gives a validator rule the code and message of a <see cref="DomainError"/>, so a rule
    /// reads the same whether the validator or the domain catches it.
    /// </summary>
    /// <remarks>
    /// Only the message reaches the client: Wolverine's problem-details middleware does not
    /// emit error codes. See "Gotchas already hit" in docs/status.md.
    /// </remarks>
    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, DomainError error) =>
        rule.WithErrorCode(error.Code).WithMessage(error.Message);
}
