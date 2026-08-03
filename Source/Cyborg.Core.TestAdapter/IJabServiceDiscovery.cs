using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.TestAdapter;

/// <summary>
/// Discovers Jab <c>SingletonAttribute&lt;T&gt;</c>, <c>ScopedAttribute&lt;T&gt;</c>, and
/// <c>TransientAttribute&lt;T&gt;</c> declarations (each in one- and two-type-argument forms) on
/// <c>[ServiceProviderModule]</c>-annotated interfaces and translates them into
/// <see cref="IServiceCollection"/> registrations via reflection.
/// </summary>
/// <remarks>
/// <para>
/// This class intentionally uses reflection, which is acceptable in the test project since AOT compilation
/// is not a constraint. It bridges the gap between Jab's compile-time DI model and MEDI's runtime model,
/// enabling the test infrastructure to replicate the same service graph that the production CLI composes
/// via Jab source generation.
/// </para>
/// </remarks>
public interface IJabServiceDiscovery
{
    /// <summary>
    /// Scans the specified Jab service provider module interface for singleton attributes
    /// (including those from <c>[Import&lt;TModule&gt;]</c>-referenced modules) and registers each discovered
    /// service as a singleton in the given <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Factory-based singletons (those with <c>Factory</c> set) invoke the static factory method on the declaring
    /// interface. Constructor-based singletons use <see cref="ActivatorUtilities"/> for instantiation.
    /// <c>[Import&lt;TModule&gt;]</c>-referenced modules are recursively processed.
    /// </remarks>
    /// <typeparam name="TModule">The Jab service provider module interface type to scan.</typeparam>
    /// <param name="services">The service collection to populate with discovered registrations.</param>
    void RegisterFromModule<TModule>(IServiceCollection services);
}
