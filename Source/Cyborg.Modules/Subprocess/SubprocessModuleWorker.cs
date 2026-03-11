using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Subprocess;

// sample subprocess module
public sealed class SubprocessModuleWorker(IWorkerContext<SubprocessModule> context, ISubprocessDispatcher dispatcher) : ModuleWorker<SubprocessModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(Module.Command.Executable, Module.Command.Arguments)
        {
            RedirectStandardOutput = Module.Output.ReadStdout,
            RedirectStandardError = Module.Output.ReadStderr,
            UseShellExecute = false,
        };
        SubprocessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        if (Module.Output.ReadStdout)
        {
            Artifacts.Expose(Module.Output.StdoutVariableName, result.StandardOutput);
        }
        if (Module.Output.ReadStderr)
        {
            Artifacts.Expose(Module.Output.StderrVariableName, result.StandardError);
        }
        return result.ExitCode == 0
            ? runtime.Success(Module, Artifacts)
            : runtime.Failure(Module, Artifacts);
    }
}
