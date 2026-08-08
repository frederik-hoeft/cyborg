using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules;

public sealed class ModuleResultBuilderFactory : IModuleResultBuilderFactory
{
    public IModuleResultBuilder CreateResultBuilder(IModuleArtifactsBuilder artifacts) => new ModuleResultBuilder(artifacts);
}

public sealed class ModuleResultBuilder(IModuleArtifactsBuilder artifacts) : IModuleResultBuilder
{
    IModuleExecutionResult<TModule> IModuleResultBuilder.Canceled<TModule>(TModule module) => CreateResult(module, ModuleExitStatus.Canceled, null);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Canceled<TModule, TResult>(TModule module, TResult result) => CreateResult(module, ModuleExitStatus.Canceled, result);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Failed<TModule>(TModule module) => CreateResult(module, ModuleExitStatus.Failed, null);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Failed<TModule, TResult>(TModule module, TResult result) => CreateResult(module, ModuleExitStatus.Failed, result);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Skipped<TModule>(TModule module) => CreateResult(module, ModuleExitStatus.Skipped, null);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Skipped<TModule, TResult>(TModule module, TResult result) => CreateResult(module, ModuleExitStatus.Skipped, result);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Success<TModule>(TModule module) => CreateResult(module, ModuleExitStatus.Success, null);

    IModuleExecutionResult<TModule> IModuleResultBuilder.Success<TModule, TResult>(TModule module, TResult result) => CreateResult(module, ModuleExitStatus.Success, result);

    IModuleExecutionResult<TModule> IModuleResultBuilder.WithStatus<TModule>(TModule module, ModuleExitStatus status) => CreateResult(module, status, null);

    IModuleExecutionResult<TModule> IModuleResultBuilder.WithStatus<TModule, TResult>(TModule module, ModuleExitStatus status, TResult result) => CreateResult(module, status, result);

    private ModuleExecutionResult<TModule> CreateResult<TModule>(TModule module, ModuleExitStatus status, IDecomposable? result) where TModule : ModuleBase, IModule<TModule>
    {
        if (result is not null)
        {
            artifacts.Expose(result);
        }
        return new ModuleExecutionResult<TModule>(module, status, artifacts);
    }
}
