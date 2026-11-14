using System.Net;
using Cortex.QuestionService.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.QuestionService.Infrastructure;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException { Error: var error })
            return false;

        var statusCode = error.Kind switch
        {
            DomainErrorKind.Validation => HttpStatusCode.BadRequest,
            DomainErrorKind.Forbidden => HttpStatusCode.Forbidden,
            DomainErrorKind.Conflict => HttpStatusCode.Conflict,
            _ => HttpStatusCode.InternalServerError
        };

        httpContext.Response.StatusCode = (int)statusCode;
        
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = (int)statusCode,
                Detail = error.Message,
                Extensions = { ["code"] = error.Code }
            }
        });
    }
}