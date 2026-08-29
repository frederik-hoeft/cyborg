using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Text;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Subprocess;

public sealed class SubprocessModuleWorker(IWorkerContext<SubprocessModule> context, IChildProcessDispatcher dispatcher) : ModuleWorker<SubprocessModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        string executable = Module.Command.Executable;
        List<TaggedString> arguments = [.. Module.Command.Arguments];
        if (Module.Impersonation is { } runUser)
        {
            executable = runUser.Executable;
            arguments =
            [
                "-u", runUser.User,
                "--", Module.Command.Executable,
                ..arguments
            ];
        }

        ChildProcessInvocation invocation = new(executable, arguments)
        {
            RedirectStandardOutput = Module.Output.ReadStdout,
            RedirectStandardError = Module.Output.ReadStderr,
            WorkingDirectory = Module.Command.WorkingDirectory,
        };
        if (Module.EnvironmentVariables is not null)
        {
            foreach (EnvironmentVariable variable in Module.EnvironmentVariables)
            {
                invocation.Environment[variable.Key] = variable.Value;
            }
        }

        ChildProcessResult executionResult = await dispatcher.ExecuteAsync(invocation, cancellationToken);
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
