using System.Diagnostics;

namespace Cyborg.Modules.Subprocess;

// sample subprocess module
public sealed class SubprocessModuleWorker(SubprocessModule module) : ModuleWorker<SubprocessModule>(module)
{
    public async override Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(Module.Executable, Module.Arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}
