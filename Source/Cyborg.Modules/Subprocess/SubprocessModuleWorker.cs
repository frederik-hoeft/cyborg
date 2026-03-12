using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Subprocess;

// sample subprocess module
public sealed class SubprocessModuleWorker(IWorkerContext<SubprocessModule> context, ISubprocessDispatcher dispatcher) : ModuleWorker<SubprocessModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(Module.Command.Executable, Module.Command.Arguments)
        {
            RedirectStandardOutput = Module.Output.ReadStdout,
            RedirectStandardError = Module.Output.ReadStderr,
            UseShellExecute = false,
        };
        SubprocessResult executionResult = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        SubprocessModuleResult result = new(executionResult.ExitCode, executionResult.StandardOutput, executionResult.StandardError);
        if (Module.CheckExitCode && result.ExitCode != 0)
        {
            return runtime.Exit(Failed(result));
        }
        return runtime.Exit(Success(result));
    }
}

[GeneratedDecomposition]
public sealed partial record SubprocessModuleResult(int ExitCode, string? Stdout, string? Stderr) : IDecomposable;