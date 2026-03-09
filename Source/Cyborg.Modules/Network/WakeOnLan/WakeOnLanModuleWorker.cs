using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Extensions;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Network.Probe;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed class WakeOnLanModuleWorker
(
    WakeOnLanModule module, 
    ISubprocessDispatcher dispatcher, 
    IPingService pingService,
    IPortProbeService portProbeService
) : ModuleWorker<WakeOnLanModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        
        string targetHost = runtime.Environment.Resolve(Module, Module.TargetHost);
        string macAddress = runtime.Environment.Resolve(Module, Module.MacAddress);
        int livenessProbePort = runtime.Environment.Resolve(Module, Module.LivenessProbePort);
        string stateVariable = runtime.Environment.Resolve(Module, Module.StateVariable);
        string executable = runtime.Environment.Resolve(Module, Module.Executable);
        string? outputEnvironmentName = runtime.Environment.Resolve(Module, Module.OutputEnvironment?.Name);
        TimeSpan maxWaitTime = runtime.Environment.Resolve(Module, Module.MaxWaitTime).OnDefault(TimeSpan.FromMinutes(5));
        TimeSpan hostDiscoveryTimeout = runtime.Environment.Resolve(Module, Module.HostDiscoveryTimeout).OnDefault(TimeSpan.FromSeconds(30));
        EnvironmentScopeReference outputRuntimeScope = runtime.Environment.Resolve(Module, Module.OutputEnvironment?.Scope).GetValueOrDefault(EnvironmentScopeReference.Current);

        IRuntimeEnvironment outputEnvironment = runtime.ResolveEnvironmentReference(new ModuleEnvironmentReference(outputRuntimeScope, outputEnvironmentName), runtime.Environment);
        bool isUp = await pingService.PingAsync(targetHost, hostDiscoveryTimeout, cancellationToken);
        if (isUp)
        {
            // don't require wake-on-lan if the host is already up, but set the state variable to false to indicate that we didn't need to wake it up
            outputEnvironment.SetVariable(stateVariable, false);
            return true;
        }
        ProcessStartInfo startInfo = new(executable, ["-i", targetHost, macAddress]);
        SubprocessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to execute wakeonlan command. Exit code: {result.ExitCode}, Standard Error: {result.StandardError}");
        }
        isUp = await portProbeService.ProbePortAsync(targetHost, livenessProbePort, maxWaitTime, cancellationToken);
        if (isUp)
        {
            // the host is now up and it wasn't before, so set the state variable to true to indicate that we had to wake it up
            outputEnvironment.SetVariable(stateVariable, true);
            return true;
        }
        // host didn't come up in time, unknown if the wake-on-lan command succeeded or not, this is a case for human investigation
        return false;
    }
}
