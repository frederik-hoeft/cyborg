using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging;

/// <summary>
/// Adds dispatch-local debugger services in front of the host's existing service provider without constructing a second DI container.
/// </summary>
internal sealed class DebugCommandServiceProvider(IDebugPauseContext pauseContext, DebugCommandResult result, IDebugReplIo io) : IServiceProvider
{
    private readonly IDebugPauseContext _pauseContext = pauseContext ?? throw new ArgumentNullException(nameof(pauseContext));
    private readonly DebugCommandResult _result = result ?? throw new ArgumentNullException(nameof(result));
    private readonly IDebugReplIo _io = io ?? throw new ArgumentNullException(nameof(io));

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }
        if (serviceType == typeof(IDebugPauseContext))
        {
            return _pauseContext;
        }
        if (serviceType == typeof(DebugCommandResult))
        {
            return _result;
        }
        if (serviceType == typeof(IDebugReplIo))
        {
            return _io;
        }

        return _pauseContext.Services.GetService(serviceType);
    }
}
