using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Network.Probe;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed class WakeOnLanModuleWorker
(
    IWorkerContext<WakeOnLanModule> context, 
    ISubprocessDispatcher dispatcher, 
    IPingService pingService,
    IPortProbeService portProbeService
) : ModuleWorker<WakeOnLanModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        bool isUp = await pingService.PingAsync(Module.TargetHost, Module.HostDiscoveryTimeout, cancellationToken);
        if (isUp)
        {
            // don't require wake-on-lan if the host is already up, but set the state variable to false to indicate that we didn't need to wake it up
            return runtime.Success(Module, Artifacts.Expose(Module.StateVariable, false));
        }
        ProcessStartInfo startInfo = new(Module.Executable, ["-i", Module.TargetHost, Module.MacAddress]);
        SubprocessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to execute wakeonlan command. Exit code: {result.ExitCode}, Standard Error: {result.StandardError}");
        }
        isUp = await portProbeService.ProbePortAsync(Module.TargetHost, Module.LivenessProbePort, Module.MaxWaitTime, cancellationToken);
        if (isUp)
        {
            // the host is now up and it wasn't before, so set the state variable to true to indicate that we had to wake it up
            return runtime.Success(Module, Artifacts.Expose(Module.StateVariable, true));
        }
        // host didn't come up in time, unknown if the wake-on-lan command succeeded or not, this is a case for human investigation
        return runtime.Failure(Module);
    }
}