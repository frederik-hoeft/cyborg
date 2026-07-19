using Cyborg.Core.Aot.Contracts;
using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Descriptors.Builders;

// generator-facing mutable builder for object properties
// hints is expandable, compile-time constant formatter metadata that can be used to influence how a property is serialized or displayed
// e.g., "secret", "hidden", "date-time-format:yyyy-MM-dd", etc.
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IObjectDescriptionBuilder)]
public interface IObjectDescriptionBuilder : IDesciptionBuilder
{
    void AddProperty<T>(string name, ImmutableArray<string> hints, T value);

    void AddObject(string name, ImmutableArray<string> hints, Action<IObjectDescriptionBuilder> describe);

    void AddCollection(string name, ImmutableArray<string> hints, Action<ICollectionDescriptionBuilder> describe);
}
