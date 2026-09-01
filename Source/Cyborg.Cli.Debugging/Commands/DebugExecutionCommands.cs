using ConsoleAppFramework;
using Cyborg.Core.Runtime.Services.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugExecutionCommands(DebugCommandResult result)
{
    /// <summary>Continue workflow execution until the next breakpoint.</summary>
    [Command("continue|c|resume")]
    public void Continue() => result.Resume(DebugResumeAction.Continue);

    /// <summary>Execute the next module on the current execution branch and break again.</summary>
    [Command("step|s")]
    public void Step() => result.Resume(DebugResumeAction.Step);

    /// <summary>Remove all breakpoints and debugger step state, then continue workflow execution.</summary>
    [Command("detach")]
    public void Detach() => result.Resume(DebugResumeAction.Detach);

    /// <summary>Cancel the current module and terminate workflow execution.</summary>
    [Command("cancel|q|quit")]
    public void Cancel() => result.Resume(DebugResumeAction.Cancel);
}
