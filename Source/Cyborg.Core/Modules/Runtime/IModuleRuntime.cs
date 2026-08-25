using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleRuntime)]
public interface IModuleRuntime
{
    IRuntimeEnvironment GlobalEnvironment { get; }

    IRuntimeEnvironment ParentEnvironment { get; }

    IRuntimeEnvironment Environment { get; }

    Task<IModuleExecutionResult> ExecuteAsync(ModuleContext moduleContext, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    Task<IModuleExecutionResult> ExecuteAsync(ModuleReference moduleReference, IRuntimeEnvironment environment, CancellationToken cancellationToken = default);

    IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null);

    IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference);

    IModuleExecutionResult Exit<TModule>(IModuleExecutionResult<TModule> result) where TModule : ModuleBase, IModuleDefinition;
}
