using Cortex.SearchService.Tests.Infrastructure;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Typesense;
using Wolverine.Tracking;

namespace Cortex.SearchService.Tests.Handlers.ErrorPolicy;

/// <summary>
/// The Search service's handler error policy (<c>WolverineSetup.ConfigureHandlerErrors</c>): what a
/// handler does when Typesense fails. Runs on <see cref="AppFixture.FaultyHost"/>, whose client throws
/// on demand, and uses the create handler, which makes exactly one Typesense call per attempt, so
/// <see cref="TypesenseFaults.Calls"/> is the number of attempts.
/// </summary>
/// <remarks>
/// <para>
/// The message is published, not invoked: an invoke only runs the retries, while a published message
/// goes through a (local, in-memory) queue, where the policy can also move it to the error queue.
/// A tracked session records that move (<c>MovedToErrorQueue</c>). Tracking follows a whole host, so
/// these tests are the only ones on the faulty host, and xUnit runs them one at a time.
/// </para>
/// <para>
/// Not covered: a transient failure that never recovers. Its pauses add up to over 20 s (1, 4 and
/// 15 s), and it was checked by hand under the AppHost (see docs/status.md).
/// </para>
/// </remarks>
public sealed class HandlerErrorPolicyTests(AppFixture fixture)
{
    public static TheoryData<string> TransientFailures => [.. Transient.Keys];
    public static TheoryData<string> PermanentFailures => [.. Permanent.Keys];

    // By name, so each case shows up as its own test.
    private static readonly Dictionary<string, Func<Exception>> Transient = new()
    {
        ["HttpRequestException"] = () => new HttpRequestException("Connection refused"),
        ["TimeoutException"] = () => new TimeoutException(),
        ["TypesenseApiServiceUnavailableException"] = () => new TypesenseApiServiceUnavailableException("Not ready"),
        // What the HTTP resilience handler from ServiceDefaults throws once its own retries are spent.
        ["TimeoutRejectedException"] = () => new TimeoutRejectedException(),
        ["BrokenCircuitException"] = () => new BrokenCircuitException()
    };

    private static readonly Dictionary<string, Func<Exception>> Permanent = new()
    {
        ["TypesenseApiBadRequestException"] = () => new TypesenseApiBadRequestException("Bad document"),
        ["TypesenseApiUnprocessableEntityException"] = () => new TypesenseApiUnprocessableEntityException("Bad field")
    };

    [Theory]
    [MemberData(nameof(TransientFailures))]
    public async Task A_transient_failure_is_retried_after_a_pause_and_the_question_is_indexed(string failure)
    {
        var (host, faults) = await FaultyHostAsync();
        var created = TestData.QuestionCreated();
        faults.FailNext(1, Transient[failure]);

        var started = TimeProvider.System.GetTimestamp();
        var session = await PublishAsync(host, created);
        var elapsed = TimeProvider.System.GetElapsedTime(started);

        session.MovedToErrorQueue.Envelopes().ShouldBeEmpty();
        faults.Calls.ShouldBe(2);
        // The pause is what sets the transient rule apart: "anything else" also retries a single
        // failure, but at once. Wolverine's exponential jitter only adds to the 1 s cooldown (the first
        // pause is 1 to 3 s), so 1 s is a floor; 0.9 leaves room for timer resolution.
        elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(0.9));
        (await fixture.GetQuestionDocumentAsync(created.QuestionId)).Title.ShouldBe(created.Title);
    }

    [Theory]
    [MemberData(nameof(PermanentFailures))]
    public async Task A_permanent_failure_goes_to_the_error_queue_without_a_retry(string failure)
    {
        var (host, faults) = await FaultyHostAsync();
        var created = TestData.QuestionCreated();
        faults.FailAlways(Permanent[failure]);

        var session = await PublishAsync(host, created);

        session.MovedToErrorQueue.SingleMessage<Cortex.Contracts.Features.Questions.QuestionCreated>()
            .QuestionId.ShouldBe(created.QuestionId);
        faults.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Any_other_failure_is_tried_three_times_then_goes_to_the_error_queue()
    {
        var (host, faults) = await FaultyHostAsync();
        var created = TestData.QuestionCreated();
        faults.FailAlways(() => new InvalidOperationException("A bug in a handler"));

        var session = await PublishAsync(host, created);

        session.MovedToErrorQueue.SingleMessage<Cortex.Contracts.Features.Questions.QuestionCreated>()
            .QuestionId.ShouldBe(created.QuestionId);
        // The first attempt and two retries.
        faults.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task A_document_Typesense_rejects_fails_with_an_exception_the_policy_treats_as_permanent()
    {
        // The other tests throw the exceptions themselves; this one checks that real Typesense really
        // answers a bad document with one of them. A null title breaks the schema (title is a string).
        var created = TestData.QuestionCreated() with { Title = null! };

        var exception = await Should.ThrowAsync<TypesenseApiException>(() => fixture.SendAsync(created));

        exception.ShouldBeOfType<TypesenseApiBadRequestException>();
    }

    private async Task<(IAlbaHost Host, TypesenseFaults Faults)> FaultyHostAsync()
    {
        var host = await fixture.FaultyHost;
        return (host, host.Services.GetRequiredService<TypesenseFaults>());
    }

    private static Task<ITrackedSession> PublishAsync(IAlbaHost host, object message) =>
        host.TrackActivity()
            // The handler is meant to throw here; by default a tracked session fails on any exception.
            .DoNotAssertOnExceptionsDetected()
            // Room for the first transient pause (1 to 3 s with jitter) and the host's code generation.
            .Timeout(TimeSpan.FromSeconds(15))
            .PublishMessageAndWaitAsync(message);
}
