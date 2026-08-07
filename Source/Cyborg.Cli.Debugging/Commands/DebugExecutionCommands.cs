using ConsoleAppFramework;
using Cyborg.Core.Modules.Debugging;

namespace Cyborg.Cli.Debugging.Commands;

internal sealed class DebugExecutionCommands(IDebugPauseContext context, DebugCommandResult result)
{
    private readonly IDebugPauseContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly DebugCommandResult _result = result ?? throw new ArgumentNullException(nameof(result));

    /// <summary>Continue workflow execution until the next breakpoint.</summary>
    [Command("continue|c|resume")]
    public void Continue() => _result.Resume(DebugResumeAction.Continue);

    /// <summary>Execute the next module and break again.</summary>
    [Command("step|s")]
    public void Step()
    {
        _context.RequestStep();
        _result.Resume(DebugResumeAction.Continue);
    }

    /// <summary>Remove all breakpoints and continue workflow execution.</summary>
    [Command("detach")]
    public void Detach()
    {
        _context.Detach();
        _result.Resume(DebugResumeAction.Continue);
    }

    /// <summary>Cancel the current module and terminate workflow execution.</summary>
    [Command("cancel|q|quit")]
    public void Cancel() => _result.Resume(DebugResumeAction.Cancel);
}
