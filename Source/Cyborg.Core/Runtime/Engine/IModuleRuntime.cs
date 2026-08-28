using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleRuntime)]
public interface IModuleRuntime
{
    IRuntimeEnvironment GlobalEnvironment { get; }

    IRuntimeEnvironment ParentEnvironment { get; }

    IRuntimeEnvironment Environment { get; }

    Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IModuleExecutionResult>> ExecuteConcurrentlyAsync(IReadOnlyList<ModuleContext> moduleContexts, CancellationToken cancellationToken = default);

    IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null);

    IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference);

    IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition;
}
