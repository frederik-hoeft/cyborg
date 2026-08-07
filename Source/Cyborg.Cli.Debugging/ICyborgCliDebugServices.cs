using Cyborg.Core.Modules.Debugging;
using Jab;

namespace Cyborg.Cli.Debugging;

[ServiceProviderModule]
[Singleton<IDebugReplIo>(Factory = nameof(CreateConsoleDebugReplIo))]
[Singleton<IDebugFrontend>(Factory = nameof(CreateConsoleDebugFrontend))]
public interface ICyborgCliDebugServices
{
    public static IDebugReplIo CreateConsoleDebugReplIo() => new ConsoleDebugReplIo();

    public static IDebugFrontend CreateConsoleDebugFrontend(IDebugReplIo io)
    {
        CafDebugCommandRouter router = new();
        DebugCommandDispatcher dispatcher = new(io, router);
        return new ConsoleDebugFrontend(io, dispatcher);
    }
}
