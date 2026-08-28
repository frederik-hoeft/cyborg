using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Runtime.Services.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IValidationResultT)]
public interface IValidationResult<out TModule> where TModule : class, IModule
{
    IReadOnlyList<ValidationError> Errors { get; }

    bool IsValid { get; }

    TModule Module { get; }

    void EnsureValid();
}
