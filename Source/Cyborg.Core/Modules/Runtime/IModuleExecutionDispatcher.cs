using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

internal interface IModuleExecutionDispatcher
{
    IModuleWorker ActivateWorker(ModuleReference moduleReference, IServiceProvider? serviceProvider);

    Task<IModuleExecutionResult> ExecuteAsync(
        IModuleWorker module,
        IModuleRuntime runtime,
        IRuntimeEnvironment environment,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken);
}
