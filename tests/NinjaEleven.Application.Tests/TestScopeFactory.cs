using Microsoft.Extensions.DependencyInjection;

namespace NinjaEleven.Application.Tests;

/// <summary>
/// The scope a service that resolves a collaborator of its own asks for.
///
/// <para>
/// Several services in the application take an <see cref="IServiceScopeFactory"/> rather than
/// the thing they need, because they work in parallel and the thing they need holds a
/// <c>DbContext</c>: one context cannot take eight streams at once, so each unit of work gets
/// a scope. A test that hands those services a collaborator directly would be testing a
/// different arrangement from the one the game runs, so the scope here resolves the very same
/// mock instances.
/// </para>
/// </summary>
internal sealed class TestScopeFactory : IServiceScopeFactory
{
    private readonly Dictionary<Type, object> _services;

    /// <summary>What each scope of this factory hands back, by the type it is asked for.</summary>
    public TestScopeFactory(params object[] services)
    {
        _services = new Dictionary<Type, object>();

        foreach (var service in services)
        {
            // Registered under the interface it was passed for as well as its own type, because
            // a service asks the container for the interface it depends on and a mock is
            // handed over as the mock.
            foreach (var contract in ContractTypesOf(service))
            {
                _services[contract] = service;
            }
        }
    }

    public IServiceScope CreateScope() => new Scope(new Provider(_services));

    /// <summary>
    /// The interfaces a mock stands in for. Moq's proxy answers to the interface it was built
    /// with, so the type of the object is not enough: a <c>Mock&lt;IMatchCleaner&gt;.Object</c>
    /// is asked for as <c>IMatchCleaner</c> and never as its concrete class.
    /// </summary>
    private static IEnumerable<Type> ContractTypesOf(object service) =>
        service.GetType().GetInterfaces().Append(service.GetType());

    /// <summary>
    /// Wraps another factory and counts the scopes handed out, for a test that wants to see that
    /// a unit of work really was opened rather than only that the football came out right.
    /// </summary>
    internal sealed class Counting : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private readonly Action _onScope;

        public Counting(IServiceScopeFactory inner, Action onScope)
        {
            _inner = inner;
            _onScope = onScope;
        }

        public IServiceScope CreateScope()
        {
            _onScope();
            return _inner.CreateScope();
        }
    }

    private sealed class Scope : IServiceScope
    {
        public Scope(IServiceProvider provider) => ServiceProvider = provider;

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            // A scope over mocks owns nothing: the objects it handed out are the ones the test
            // is holding, and disposing them here would dispose the test's own collaborators.
        }
    }

    private sealed class Provider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public Provider(Dictionary<Type, object> services) => _services = services;

        public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);

        /// <summary>
        /// The same answer as <see cref="GetService"/>, and it throws for a type the factory was
        /// not built with rather than answering null — a service asking for something the test
        /// forgot to register should say so.
        /// </summary>
        public object GetRequiredService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : throw new InvalidOperationException(
                    $"The test scope has no {serviceType.Name}. Register it in the TestScopeFactory the service is built with.");
    }
}
