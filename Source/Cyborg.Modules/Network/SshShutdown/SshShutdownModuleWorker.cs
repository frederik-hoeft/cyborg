using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Network.SshShutdown;

public sealed class SshShutdownModuleWorker(IWorkerContext<SshShutdownModule> context, IChildProcessDispatcher dispatcher) : ModuleWorker<SshShutdownModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        Logger.LogSshShutdownSending(Module.Hostname);
        List<string> sshArguments = ["-p", Module.Port.ToString(), $"{Module.Username}@{Module.Hostname}", Module.ShutdownCommand];
        (string executable, List<string> arguments) = Module.SshPass switch
        {
            { MatchPrompt: not null } sshPass => (sshPass.Executable, [ $"-f{sshPass.FilePath}", "-P", sshPass.MatchPrompt, Module.Executable, ..sshArguments]),
            { MatchPrompt: null } sshPass => (sshPass.Executable, [ $"-f{sshPass.FilePath}", Module.Executable, ..sshArguments]),
            _ => (Module.Executable, sshArguments),
        };
        ProcessStartInfo startInfo = new(executable, arguments);
        ChildProcessResult processResult = await dispatcher.ExecuteAsync(startInfo, cancellationToken);
        SshShutdownModuleResult result = new(processResult.ExitCode, processResult.StandardOutput, processResult.StandardError);
        if (result.ExitCode != 0)
        {
            Logger.LogSshShutdownFailed(Module.Hostname, result.ExitCode);
            return runtime.Exit(Failed(result));
        }
        Logger.LogSshShutdownSucceeded(Module.Hostname);
        return runtime.Exit(Success(result));
    }
}