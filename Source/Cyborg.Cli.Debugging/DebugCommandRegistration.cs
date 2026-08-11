using ConsoleAppFramework;
using Cyborg.Cli.Debugging.Commands;

namespace Cyborg.Cli.Debugging;

internal static class DebugCommandRegistration
{
    internal static void Register(ConsoleApp.ConsoleAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Add<DebugExecutionCommands>();
        app.Add<DebugInspectionCommands>();
        app.Add<DebugBreakpointCommands>();
    }
}
