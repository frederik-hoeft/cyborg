using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;

namespace Cyborg.Modules.Subprocess;

// sample subprocess module
public sealed class SubprocessModuleWorker(SubprocessModule module, ISubprocessDispatcher dispatcher) : ModuleWorker<SubprocessModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        SubprocessCommand? command = Module.Command
            ?? throw new InvalidOperationException("SubprocessModule requires a Command to be specified.");
        SubprocessOutputOptions output = Module.Output ?? new SubprocessOutputOptions();
        ProcessStartInfo startInfo = new(command.Executable, command.Arguments)
        {
            RedirectStandardOutput = output.ReadStdout,
            RedirectStandardError = output.ReadStderr,
            UseShellExecute = false,
        };
        SubprocessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        if (output.ReadStdout)
        {
            runtime.Environment.SetVariable(CreateVariableName(output, SubprocessModule.StandardOutputName), result.StandardOutput);
        }
        if (output.ReadStderr)
        {
            runtime.Environment.SetVariable(CreateVariableName(output, SubprocessModule.StandardErrorName), result.StandardError);
        }
        return result.ExitCode == 0;
    }

    private static string CreateVariableName(SubprocessOutputOptions output, string type)
    {
        if (string.IsNullOrWhiteSpace(output.Namespace))
        {
            return type;
        }
        return $"{output.Namespace}.{type}";
    }
}
