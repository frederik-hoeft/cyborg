using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Network;
using Cyborg.Core.Services.Subprocesses;
using System.Diagnostics;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed record WakeOnLanModule
(
    string TargetHost,
    string MacAddress,
    int LivenessProbePort,
    string StateVariable,
    TimeSpan MaxWaitTime,
    TimeSpan HostDiscoveryTimeout,
    ModuleEnvironmentReference? OutputEnvironment,
    string Executable = "/usr/bin/wakeonlan"
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.wol.v1";
}

public sealed class WakeOnLanModuleWorker(WakeOnLanModule module, ISubprocessDispatcher dispatcher, IPingService pingService) : ModuleWorker<WakeOnLanModule>(module)
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
        TimeSpan maxWaitTime = runtime.Environment.Resolve(Module, Module.MaxWaitTime);
        TimeSpan hostDiscoveryTimeout = runtime.Environment.Resolve(Module, Module.HostDiscoveryTimeout);
        EnvironmentScopeReference? outputRuntimeScope = runtime.Environment.Resolve(Module, Module.OutputEnvironment?.Scope);
        if (maxWaitTime == default)
        {
            maxWaitTime = TimeSpan.FromMinutes(5);
        }
        if (hostDiscoveryTimeout == default)
        {
            hostDiscoveryTimeout = TimeSpan.FromSeconds(30);
        }
        IRuntimeEnvironment outputEnvironment = runtime.ResolveEnvironmentReference(new ModuleEnvironmentReference(outputRuntimeScope ?? EnvironmentScopeReference.Current, outputEnvironmentName))
            ?? runtime.Environment;
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
        // TODO: wait for the host to come up
        outputEnvironment.SetVariable(stateVariable, true);
        return true;
    }
}

[GeneratedModuleLoaderFactory]
public sealed partial class WakeOnLanModuleLoader(IServiceProvider serviceProvider) : ModuleLoader<WakeOnLanModuleWorker, WakeOnLanModule>(serviceProvider);