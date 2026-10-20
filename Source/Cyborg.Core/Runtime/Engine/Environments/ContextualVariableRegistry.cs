using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using System.Collections.Frozen;

namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// Resolves runtime-owned contextual names separately from transactional environment variables.
/// The reserved <c>@.</c> syntax is independent of the registered properties: an unknown or
/// unavailable property is undefined in the selected view and never falls back to its parent.
/// </summary>
internal sealed class ContextualVariableRegistry
{
    private readonly FrozenDictionary<string, Func<EnvironmentContext, string, object?>> _resolvers;

    internal static ContextualVariableRegistry Default { get; } = new(
    [
        new("@", static (_, ns) => ns),
        new("@.origin.file", static (context, _) => context.Origin?.File),
        new("@.origin.directory", static (context, _) => context.Origin?.Directory)
    ]);

    internal ContextualVariableRegistry(IEnumerable<Registration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _resolvers = registrations.ToFrozenDictionary(static registration => registration.Name, static registration => registration.Resolve, StringComparer.Ordinal);
        if (_resolvers.Keys.Any(static name => !IsContextualName(name)))
        {
            throw new ArgumentException("Contextual registrations must use a reserved contextual name.", nameof(registrations));
        }
    }

    /// <summary>Returns a new registry with an additional contextual binding; the existing registry is unchanged.</summary>
    internal ContextualVariableRegistry With(Registration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new ContextualVariableRegistry(_resolvers.Select(static pair => new Registration(pair.Key, pair.Value)).Append(registration));
    }

    internal static bool IsContextualName(string name) => ContextualVariableSyntax.IsContextualName(name);

    internal bool TryResolve(string name, EnvironmentContext context, string ns, out object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        value = _resolvers.TryGetValue(name, out Func<EnvironmentContext, string, object?>? resolve) ? resolve(context, ns) : null;
        return value is not null;
    }

    internal sealed record Registration(string Name, Func<EnvironmentContext, string, object?> Resolve);
}
