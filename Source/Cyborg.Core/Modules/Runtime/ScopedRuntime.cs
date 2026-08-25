using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

internal sealed class ScopedRuntime(
    IModuleRuntime root,
    IModuleRuntime parent,
    RuntimeEnvironmentContext environmentContext,
    ILoggerFactory loggerFactory,
    IServiceProvider? serviceProvider)
    : ModuleRuntimeBase(environmentContext, loggerFactory, serviceProvider)
{
    [NotNull]
    protected override IModuleRuntime? Parent => parent;

    protected override Task<IModuleExecutionResult> ExecuteWorkerAsync(
        IModuleWorker module,
        IRuntimeEnvironment environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        return ExecuteModuleAsync(root, module, environment, cancellationToken);
    }
}
