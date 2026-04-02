using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Subprocess;

public sealed class SubprocessModuleWorker(IWorkerContext<SubprocessModule> context, IChildProcessDispatcher dispatcher) : ModuleWorker<SubprocessModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        string executable = runtime.Environment.Interpolate(Module.Command.Executable);
        ImmutableArray<string> arguments = [.. Module.Command.Arguments.Select(runtime.Environment.Interpolate)];
        if (Module.Impersonation is { } runUser)
        {
            executable = runtime.Environment.Interpolate(runUser.Executable);
            arguments =
            [
                "-u", runtime.Environment.Interpolate(runUser.User),
                "--", runtime.Environment.Interpolate(Module.Command.Executable),
                ..arguments
            ];
        }
        ProcessStartInfo startInfo = new(executable, arguments)
        {
            RedirectStandardOutput = Module.Output.ReadStdout,
            RedirectStandardError = Module.Output.ReadStderr,
        };
        ChildProcessResult executionResult = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        SubprocessModuleResult result = new(executionResult.ExitCode, executionResult.StandardOutput, executionResult.StandardError);
        if (Module.CheckExitCode && result.ExitCode != 0)
        {
            Logger.LogSubprocessFailed(executable, result.ExitCode);
            return runtime.Exit(Failed(result));
        }
        return runtime.Exit(Success(result));
    }
}

[GeneratedDecomposition]
public sealed partial record SubprocessModuleResult(int ExitCode, string? Stdout, string? Stderr) : IDecomposable;