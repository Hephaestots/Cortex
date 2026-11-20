using Cortex.QuestionService.Data;
using Cortex.QuestionService.Domain;
using Cortex.QuestionService.Features.Questions;
using Cortex.QuestionService.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine.Http;

namespace Cortex.QuestionService.Features.Answers;

public static class CreateAnswerEndpoint
{
    public static Task<Question?> LoadAsync(Guid id, QuestionDbContext db, CancellationToken ct) =>
        db.Questions
            .FirstOrDefaultAsync(question => question.Id == id, ct);

    public static IResult Validate(Question? question)
    {
        if (question is null)
            return TypedResults.NotFound();

        return WolverineContinue.Result();
    }
    
    [Authorize]
    [WolverinePost("/api/questions/{id:guid}/answers")]
    public static async Task<Results<Created<CreateAnswerResponse>, ValidationProblem>> Post(
        CreateAnswerRequest request,
        CurrentUser user,
        Question question, 
        QuestionDbContext db, 
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var answer = question.AddAnswer(
            request.Body, 
            user.Id, 
            user.DisplayName, 
            timeProvider.GetUtcNow());
        
        await db.SaveChangesAsync(ct);
        
        return TypedResults.Created(
            uri: $"/api/answers/{answer.Id}",
            CreateAnswerResponse.FromAnswer(answer));
    }
}

public sealed record CreateAnswerRequest(string Body)
{
    public class CreateAnswerValidator : AbstractValidator<CreateAnswerRequest>
    {
        public CreateAnswerValidator()
        {
            RuleFor(x => x.Body)
                .NotEmpty()
                .WithError(Answer.Errors.EmptyBody)
                .MaximumLength(Answer.MaxBodyLength)
                .WithError(Answer.Errors.BodyTooLong);
        }
    }
}

public sealed record CreateAnswerResponse(
    Guid Id,
    Guid QuestionId,
    string Body,
    string AuthorId,
    string AuthorDisplayName,
    DateTimeOffset CreatedAt,
    uint Version)
{
    public static CreateAnswerResponse FromAnswer(Answer answer) => new(
        answer.Id,
        answer.QuestionId,
        answer.Body,
        answer.AuthorId,
        answer.AuthorDisplayName,
        answer.CreatedAt,
        answer.Version);
}