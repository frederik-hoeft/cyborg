using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Core.Services.Network.Probe;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed class WakeOnLanModuleWorker(IWorkerContext<WakeOnLanModule> context, IChildProcessDispatcher dispatcher, IPingService pingService, IPortProbeService portProbeService) : ModuleWorker<WakeOnLanModule>(context)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        Logger.LogWolCheckingReachability(Module.TargetHost, Module.HostDiscoveryTimeout.ToString());
        bool isUp = await pingService.PingAsync(Module.TargetHost, Module.HostDiscoveryTimeout, cancellationToken);
        if (isUp)
        {
            Logger.LogWolHostAlreadyUp(Module.TargetHost);
            // don't require wake-on-lan if the host is already up, but set the state variable to false to indicate that we didn't need to wake it up
            return runtime.Exit(Success(new WakeOnLanModuleResult(WokeUp: false)));
        }
        Logger.LogWolSendingPacket(Module.TargetHost, Module.MacAddress);
        ProcessStartInfo startInfo = new(Module.Executable, ["-i", Module.TargetHost, Module.MacAddress]);
        ChildProcessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            Logger.LogWolCommandFailed(result.ExitCode, result.StandardError);
            throw new InvalidOperationException($"Failed to execute wakeonlan command. Exit code: {result.ExitCode}, Standard Error: {result.StandardError}");
        }
        Logger.LogWolProbingHost(Module.TargetHost, Module.LivenessProbePort, Module.MaxWaitTime.ToString());
        isUp = await portProbeService.ProbePortAsync(Module.TargetHost, Module.LivenessProbePort, Module.MaxWaitTime, cancellationToken);
        if (isUp)
        {
            Logger.LogWolHostCameOnline(Module.TargetHost);
            // the host is now up and it wasn't before, so set the state variable to true to indicate that we had to wake it up
            return runtime.Exit(Success(new WakeOnLanModuleResult(WokeUp: true)));
        }
        Logger.LogWolHostTimeout(Module.TargetHost);
        // host didn't come up in time, unknown if the wake-on-lan command succeeded or not, this is a case for human investigation
        return runtime.Exit(Failed(new WakeOnLanModuleResult(WokeUp: false)));
    }
}