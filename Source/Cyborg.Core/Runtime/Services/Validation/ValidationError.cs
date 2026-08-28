using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Runtime.Services.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ValidationError)]
public sealed record ValidationError(string PropertyName, string Rule, string Message);
