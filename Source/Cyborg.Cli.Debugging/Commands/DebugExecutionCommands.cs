using ConsoleAppFramework;
using Cyborg.Core.Runtime.Services.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugExecutionCommands(IDebugPauseContext context, DebugCommandResult result)
{
    /// <summary>Continue workflow execution until the next breakpoint.</summary>
    [Command("continue|c|resume")]
    public void Continue() => result.Resume(DebugResumeAction.Continue);

    /// <summary>Execute the next module and break again.</summary>
    [Command("step|s")]
    public void Step()
    {
        context.RequestStep();
        result.Resume(DebugResumeAction.Continue);
    }

    /// <summary>Remove all breakpoints and continue workflow execution.</summary>
    [Command("detach")]
    public void Detach()
    {
        context.Detach();
        result.Resume(DebugResumeAction.Continue);
    }

    /// <summary>Cancel the current module and terminate workflow execution.</summary>
    [Command("cancel|q|quit")]
    public void Cancel() => result.Resume(DebugResumeAction.Cancel);
}
