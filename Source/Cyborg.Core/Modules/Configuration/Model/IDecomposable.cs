using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Configuration.Model;

[GeneratorContractRegistration<ModelDecompositionGeneratorContract>(ModelDecompositionGeneratorContract.IDecomposable)]
public interface IDecomposable
{
    IEnumerable<DynamicKeyValuePair> Decompose();
}
