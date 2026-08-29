using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Services.Default;
using Jab;

namespace Cyborg.Cli.Debugging;

[ServiceProviderModule]
[Singleton<IDebugReplIo>(Factory = nameof(CreateConsoleDebugReplIo))]
[Singleton<IDebugFrontend>(Factory = nameof(CreateConsoleDebugFrontend))]
[Singleton<ICliDebugArgumentHandler>(Factory = nameof(CreateDebugArgumentHandler))]
public interface ICyborgCliDebugServices
{
    static IDebugReplIo CreateConsoleDebugReplIo() => new ConsoleDebugReplIo();

    static IDebugFrontend CreateConsoleDebugFrontend(IDebugReplIo io)
    {
        CafDebugCommandRouter router = new();
        DebugCommandDispatcher dispatcher = new(io, router);
        return new ConsoleDebugFrontend(io, dispatcher);
    }

    static ICliDebugArgumentHandler CreateDebugArgumentHandler(IBreakpointRegistry breakpoints, IDefault<IDebugFrontend> defaultFrontend) =>
        new CliDebugArgumentHandler(breakpoints, defaultFrontend);
}
