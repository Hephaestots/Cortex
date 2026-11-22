using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Typesense;

namespace Cortex.SearchService.Tests.Infrastructure;

/// <summary>
/// Makes the Typesense client of <see cref="AppFixture.FaultyHost"/> throw on demand, so the error
/// policy tests can trigger each kind of failure without stopping the shared container (which the
/// other tests need, and which would be slow: the HTTP resilience handler retries for 30 s).
/// </summary>
/// <remarks>
/// Counts every call to the client, so a test can tell how many times the handler ran. Only the
/// error policy tests use the faulty host, and xUnit runs the tests of one class one at a time, so
/// the counter and the armed fault never mix two tests.
/// </remarks>
public sealed class TypesenseFaults
{
    private readonly Lock _lock = new();
    private Func<Exception>? _fault;
    private int _remaining;
    private int _calls;

    public int Calls
    {
        get { lock (_lock) return _calls; }
    }

    /// <summary>The next <paramref name="times"/> calls throw what <paramref name="fault"/> builds.</summary>
    public void FailNext(int times, Func<Exception> fault)
    {
        lock (_lock)
        {
            _fault = fault;
            _remaining = times;
            _calls = 0;
        }
    }

    public void FailAlways(Func<Exception> fault) => FailNext(int.MaxValue, fault);

    internal Exception? NextFault()
    {
        lock (_lock)
        {
            _calls++;

            if (_remaining == 0 || _fault is null)
                return null;

            _remaining--;
            return _fault();
        }
    }
}

/// <summary>
/// A stand-in for <see cref="ITypesenseClient"/> that asks <see cref="TypesenseFaults"/> before each
/// call: it throws the armed exception, or passes the call to the real client. A
/// <see cref="DispatchProxy"/>, so it covers the interface's many methods without listing them.
/// </summary>
public class FaultyTypesenseClient : DispatchProxy
{
    private ITypesenseClient _inner = null!;
    private TypesenseFaults _faults = null!;

    public static ITypesenseClient Create(ITypesenseClient inner, TypesenseFaults faults)
    {
        var proxy = Create<ITypesenseClient, FaultyTypesenseClient>();
        var self = (FaultyTypesenseClient)(object)proxy;
        self._inner = inner;
        self._faults = faults;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        // Thrown before the call, as an unreachable Typesense would fail it; the handler's await sees
        // the same exception either way.
        if (_faults.NextFault() is { } fault)
            throw fault;

        try
        {
            return method!.Invoke(_inner, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(e.InnerException);
            throw;
        }
    }
}

public static class FaultyTypesenseServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="ITypesenseClient"/> in a <see cref="FaultyTypesenseClient"/>.
    /// <c>AddTypesenseClient</c> registers the client twice and the last one wins (a typed
    /// <c>HttpClient</c> factory), so that one is kept as the inner client, with its lifetime.
    /// </summary>
    public static IServiceCollection AddTypesenseFaults(this IServiceCollection services)
    {
        var original = services.Last(descriptor => descriptor.ServiceType == typeof(ITypesenseClient));

        services.RemoveAll<ITypesenseClient>();
        services.AddSingleton<TypesenseFaults>();
        services.Add(new ServiceDescriptor(
            typeof(ITypesenseClient),
            provider => FaultyTypesenseClient.Create(
                (ITypesenseClient)Resolve(original, provider),
                provider.GetRequiredService<TypesenseFaults>()),
            original.Lifetime));

        return services;
    }

    private static object Resolve(ServiceDescriptor descriptor, IServiceProvider provider) =>
        descriptor.ImplementationFactory?.Invoke(provider)
        ?? descriptor.ImplementationInstance
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}
