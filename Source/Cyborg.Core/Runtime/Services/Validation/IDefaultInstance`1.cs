using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Runtime.Services.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IDefaultValueT)]
public interface IDefaultInstance<TSelf> where TSelf : class, IDefaultInstance<TSelf>
{
    static abstract TSelf Default { get; }
}
