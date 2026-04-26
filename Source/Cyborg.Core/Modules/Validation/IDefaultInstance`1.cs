using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Validation;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IDefaultValueT)]
public interface IDefaultInstance<TSelf> where TSelf : class, IDefaultInstance<TSelf>
{
    static abstract TSelf Default { get; }
}
