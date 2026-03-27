using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Modules.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Subprocess;

public sealed class SubprocessModuleWorker(IWorkerContext<SubprocessModule> context, IChildProcessDispatcher dispatcher, ILoggerFactory loggerFactory) : ModuleWorker<SubprocessModule>(context)
{
    private readonly ILogger<SubprocessModuleWorker> _logger = loggerFactory.CreateLogger<SubprocessModuleWorker>();

    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        string executable = Module.Command.Executable;
        ImmutableArray<string> arguments = Module.Command.Arguments;
        if (Module.Impersonation is { } runUser)
        {
            executable = runUser.Executable;
            arguments =
            [
                "-u", runUser.User,
                "--", Module.Command.Executable,
                ..Module.Command.Arguments
            ];
        }
        _logger.LogSubprocessStarted(executable, arguments.Length);
        ProcessStartInfo startInfo = new(executable, arguments)
        {
            RedirectStandardOutput = Module.Output.ReadStdout,
            RedirectStandardError = Module.Output.ReadStderr,
        };
        ChildProcessResult executionResult = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        SubprocessModuleResult result = new(executionResult.ExitCode, executionResult.StandardOutput, executionResult.StandardError);
        if (Module.CheckExitCode && result.ExitCode != 0)
        {
            _logger.LogSubprocessFailed(executable, result.ExitCode);
            return runtime.Exit(Failed(result));
        }
        _logger.LogSubprocessCompleted(executable, result.ExitCode);
        return runtime.Exit(Success(result));
    }
}

[GeneratedDecomposition]
public sealed partial record SubprocessModuleResult(int ExitCode, string? Stdout, string? Stderr) : IDecomposable;