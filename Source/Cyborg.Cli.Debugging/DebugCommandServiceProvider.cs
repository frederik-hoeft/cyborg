using Cyborg.Core.Modules.Debugging;
using System.Collections.Frozen;
using DIKvp = (System.Type Key, object? Value);

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Adds dispatch-local debugger services in front of the host's existing service provider without constructing a second DI container.
/// </summary>
internal sealed class DebugCommandServiceProvider(IDebugPauseContext pauseContext, DebugCommandResult result, IDebugReplIo io) : IServiceProvider
{
    private readonly FrozenDictionary<Type, object?> _injectedServices = ((DIKvp[])
    [
        SingletonOf(pauseContext),
        SingletonOf(result),
        SingletonOf(io),
        SingletonOf(pauseContext.ValidationResult),
        SingletonOf(pauseContext.ValidationResult.Module),
        SingletonOf(pauseContext.ValidationResult.Errors),
    ]).ToFrozenDictionary(static kvp => kvp.Key, static kvp => kvp.Value);

    private static DIKvp SingletonOf<T>(T instance) where T : class => (typeof(T), instance ?? throw new ArgumentNullException(nameof(instance)));

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }
        if (_injectedServices.TryGetValue(serviceType, out object? service))
        {
            return service;
        }
        return pauseContext.Services.GetService(serviceType);
    }
}
