using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider = null)
    : ModuleRuntimeBase(RuntimeEnvironmentContext.CreateRoot(defaultEnvironment, loggerFactory), loggerFactory, serviceProvider)
{
    protected override IModuleRuntime? Parent => null;

    protected override Task<IModuleExecutionResult> ExecuteWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        return ExecuteModuleAsync(root: this, module, environment, cancellationToken);
    }
}
