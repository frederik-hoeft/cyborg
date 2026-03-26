using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics;

public interface IDynamicGenericValueProviderFactory
{
    string TypeName { get; }

    int Arity { get; }

    bool TryCreateProvider(
        ImmutableArray<IDynamicValueProvider> typeArguments,
        [NotNullWhen(true)] out IDynamicValueProvider? provider);
}