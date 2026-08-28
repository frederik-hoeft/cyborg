using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

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
