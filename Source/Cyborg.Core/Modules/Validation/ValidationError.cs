using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ValidationError)]
public sealed record ValidationError(string PropertyName, string Rule, string Message);
