using Cyborg.Core.Aot.Contracts;
using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IObjectDescriptionBuilder)]
public interface IObjectDescriptionBuilder
{
    void AddProperty<T>(string name, T value, ImmutableArray<string> hints = default);

    void AddObject(string name, Action<IObjectDescriptionBuilder> describe, ImmutableArray<string> hints = default);

    void AddCollection(string name, Action<ICollectionDescriptionBuilder> describe, ImmutableArray<string> hints = default);
}
