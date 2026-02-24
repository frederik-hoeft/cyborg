using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using System.Diagnostics;

namespace Cyborg.Modules.Subprocess;

// sample subprocess module
public sealed class SubprocessModuleWorker(SubprocessModule module) : ModuleWorker<SubprocessModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        SubprocessCommand? command = Module.Command
            ?? throw new InvalidOperationException("SubprocessModule requires a Command to be specified.");
        SubprocessOutputOptions output = Module.Output ?? new SubprocessOutputOptions();
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(command.Executable, command.Arguments)
            {
                RedirectStandardOutput = output.ReadStdout,
                RedirectStandardError = output.ReadStderr,
                UseShellExecute = false,
            },
        };
        process.Start();
        List<Task<CommandOutput>> ioTasks = [];
        if (output.ReadStdout)
        {
            ioTasks.Add(ReadStreamAsync(process.StandardOutput, SubprocessModule.StandardOutputName, cancellationToken));
        }
        if (output.ReadStderr)
        {
            ioTasks.Add(ReadStreamAsync(process.StandardError, SubprocessModule.StandardErrorName, cancellationToken));
        }
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(ioTasks);
        foreach (CommandOutput io in ioTasks.Select(t => t.Result))
        {
            string variable = CreateVariableName(output, io.Type);
            runtime.Environment.SetVariable(variable, io.Data);
        }
        return process.ExitCode == 0;
    }

    private static string CreateVariableName(SubprocessOutputOptions output, string type)
    {
        if (string.IsNullOrWhiteSpace(output.Namespace))
        {
            return type;
        }
        return $"{output.Namespace}.{type}";
    }

    private static async Task<CommandOutput> ReadStreamAsync(StreamReader reader, string type, CancellationToken cancellationToken)
    {
        string data = await reader.ReadToEndAsync(cancellationToken);
        return new CommandOutput(data, type);
    }

    private sealed record CommandOutput(string Data, string Type);
}
