using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Network.Probe;
using Cyborg.Core.Services.Subprocesses;
using Cyborg.Modules.Shared.Extensions;
using System.Diagnostics;

namespace Cyborg.Modules.Network.WakeOnLan;

public sealed class WakeOnLanModuleWorker
(
    WakeOnLanModule module, 
    ISubprocessDispatcher dispatcher, 
    IPingService pingService,
    IPortProbeService portProbeService,
    IServiceProvider serviceProvider
) : ModuleWorker<WakeOnLanModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        ValidationResult<WakeOnLanModule> moduleValidation = await Module.ValidateAsync(runtime, serviceProvider, cancellationToken);
        if (moduleValidation is not { IsValid: true, Module: { } module })
        {
            return false;
        }

        IRuntimeEnvironment outputEnvironment = runtime.ResolveEnvironmentReference(module.OutputEnvironment, runtime.Environment);
        bool isUp = await pingService.PingAsync(module.TargetHost, module.HostDiscoveryTimeout, cancellationToken);
        if (isUp)
        {
            // don't require wake-on-lan if the host is already up, but set the state variable to false to indicate that we didn't need to wake it up
            outputEnvironment.SetVariable(module.StateVariable, false);
            return true;
        }
        ProcessStartInfo startInfo = new(module.Executable, ["-i", module.TargetHost, module.MacAddress]);
        SubprocessResult result = await dispatcher.ExecuteAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to execute wakeonlan command. Exit code: {result.ExitCode}, Standard Error: {result.StandardError}");
        }
        isUp = await portProbeService.ProbePortAsync(module.TargetHost, module.LivenessProbePort, module.MaxWaitTime, cancellationToken);
        if (isUp)
        {
            // the host is now up and it wasn't before, so set the state variable to true to indicate that we had to wake it up
            outputEnvironment.SetVariable(module.StateVariable, true);
            return true;
        }
        // host didn't come up in time, unknown if the wake-on-lan command succeeded or not, this is a case for human investigation
        return false;
    }
}
