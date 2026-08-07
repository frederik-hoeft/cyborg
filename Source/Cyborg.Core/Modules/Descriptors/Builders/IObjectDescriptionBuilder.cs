using Cyborg.Core.Aot.Contracts;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IObjectDescriptionBuilder)]
public interface IObjectDescriptionBuilder
{
    void AddProperty<T>(string name, ImmutableArray<string> hints, T value);

    void AddObject(string name, ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe);

    void AddCollection(string name, ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe);
}
