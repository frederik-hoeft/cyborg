using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Descriptors;
using Jab;

namespace Cyborg.Cli.Debugging;

[ServiceProviderModule]
[Singleton<IDebugFrontend>(Factory = nameof(CreateConsoleDebugFrontend))]
public interface ICyborgCliDebugServices
{
    public static IDebugFrontend CreateConsoleDebugFrontend(IModuleSerializationService serializationService)
    {
        ConsoleDebugReplIo io = new();
        DebugCommandDispatcher dispatcher = new(io, serializationService);
        return new ConsoleDebugFrontend(io, dispatcher);
    }
}
