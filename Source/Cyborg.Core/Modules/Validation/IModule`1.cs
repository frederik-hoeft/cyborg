using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IModuleT)]
public interface IModule<TSelf> : IModule where TSelf : class, IModule<TSelf>
{
    /// <summary>
    /// Resolves any applicable overrides for the current module instance asynchronously using the specified generated validation context and service provider.
    /// </summary>
    /// <param name="runtime">The runtime in which the module operates.</param>
    /// <param name="validationContext">The generated-pipeline context used to select and resolve property overrides.</param>
    /// <param name="serviceProvider">The service provider that supplies required services for resolving overrides.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task whose result contains the current module instance with applicable overrides applied.</returns>
    ValueTask<TSelf> ResolveOverridesAsync(IModuleRuntime runtime, GeneratedModuleValidationContext validationContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Applies default values to the module instance based on model annotations and the provided runtime environment.
    /// </summary>
    /// <param name="runtime">The runtime environment that determines which default settings are applicable to the module.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies and services required for applying defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation before completion.</param>
    /// <returns>A value task whose result contains the module instance after defaults have been applied.</returns>
    ValueTask<TSelf> ApplyDefaultsAsync(IModuleRuntime runtime, IServiceProvider serviceProvider, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the current instance asynchronously using the specified runtime environment and generated validation context.
    /// </summary>
    /// <remarks>
    /// Performs the generated preparation and validation pipeline in the following order: apply defaults, resolve overrides,
    /// reapply defaults to overridden values, interpolate eligible strings, and validate constraints.
    /// </remarks>
    /// <param name="runtime">The runtime environment that provides context and resources required for validation.</param>
    /// <param name="validationContext">The generated-pipeline context that mediates preparation-only runtime operations.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies needed during validation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel validation.</param>
    /// <returns>A task whose result indicates whether the transformed module is valid.</returns>
    ValueTask<ValidationResult<TSelf>> ValidateAsync(IModuleRuntime runtime, GeneratedModuleValidationContext validationContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
