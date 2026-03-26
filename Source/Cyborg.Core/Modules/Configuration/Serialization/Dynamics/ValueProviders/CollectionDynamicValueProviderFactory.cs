using System.Collections.Immutable;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

public sealed class CollectionDynamicValueProviderFactory : IDynamicGenericValueProviderFactory
{
    public string TypeName => "collection";

    public int Arity => 1;

    public bool TryCreateProvider(ImmutableArray<IDynamicValueProvider> typeArguments, [NotNullWhen(true)] out IDynamicValueProvider? provider)
    {
        if (typeArguments.Length != 1)
        {
            provider = null;
            return false;
        }

        provider = new CollectionDynamicValueProvider(typeArguments[0]);
        return true;
    }
}